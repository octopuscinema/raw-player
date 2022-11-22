using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.Playback
{
	public class PlaybackCinemaDNG : Playback
	{
        private static readonly uint bufferDurationFrames = 6;
        private static readonly uint bufferSizeFrames = 12;

        private Worker<Error> SeekWork { get; set; }
        private SequenceFrameDNG SeekFrame { get; set; }
        private Mutex SeekFrameMutex { get; set; }

        private ISequenceStream SequenceStream { get; set; }
        private IShader GpuPipelineProgram { get; set; }
        private ITexture LinearizeTable { get; set; }

        public override event EventHandler ClipOpened;
        public override event EventHandler ClipClosed;

        public override List<Essence> SupportedEssence { get { return new List<Essence>() { Essence.Sequence }; } }

        public override uint FirstFrame { get { return ((IO.DNG.MetadataCinemaDNG)Clip.Metadata).FirstFrame; } }
        public override uint LastFrame { get { return ((IO.DNG.MetadataCinemaDNG)Clip.Metadata).LastFrame; } }

        public override uint? ActiveSeekRequest { get; protected set; }

        byte[] displayFrameStaging;
        ITexture displayFrameGPU;
        

        public PlaybackCinemaDNG(IPlayerWindow playerWindow, GPU.Render.IContext renderContext)
            : base(playerWindow, renderContext, bufferDurationFrames)
        {
            SeekFrameMutex = new Mutex();
        }

        public override void Dispose()
        {
            base.Dispose();
            SeekFrameMutex.Dispose();
            SeekFrameMutex = null;
        }

        public override void Close()
        {
            RenderContext.ClearRenderActions();
            if (State != State.Stopped && State != State.Empty)
                Stop();
            Debug.Assert(IsOpen());
            if (SeekFrame != null)
            {
                SeekFrame.Dispose();
                SeekFrame = null;
            }
            if (SequenceStream != null)
            {
                SequenceStream.Dispose();
                SequenceStream = null;
            }
            if (displayFrameGPU != null)
            {
                displayFrameGPU.Dispose();
                displayFrameGPU = null;
            }
            if(LinearizeTable != null)
            {
                LinearizeTable.Dispose();
                LinearizeTable = null;
            }
            displayFrameStaging = null;
            State = State.Empty;
            Clip = null;
            LastDisplayedFrame = null;
            ClipClosed?.Invoke(this, new EventArgs());
            GC.Collect();
        }

        public override Error Open(IClip clip)
        {
            Debug.Assert(State == State.Empty);
            Debug.Assert(!IsOpen());
            if (IsOpen())
                Close();

            // Load metadata, if that was unsuccesful, bail out
            var cinemaDNGClip = (ClipCinemaDNG)clip;
            Debug.Assert(cinemaDNGClip != null);
            if (clip.ReadMetadata() != Error.None)
                return Error.BadMetadata;
            Clip = clip;
            LastDisplayedFrame = FirstFrame;
            var cinemaDNGMetadata = (IO.DNG.MetadataCinemaDNG)cinemaDNGClip.Metadata;

            // Attempt to decode first frame as preview, if that fails bail out
            var gpuFormat = clip.Metadata.DecodedBitDepth <= 8 ? GPU.Render.TextureFormat.R8 : GPU.Render.TextureFormat.R16;
            var previewFrame = new SequenceFrameDNG(RenderContext, clip, gpuFormat);
            previewFrame.frameNumber = cinemaDNGMetadata.FirstFrame;
            var decodeError = previewFrame.Decode(clip);
            if (decodeError != Error.None)
            {
                previewFrame.Dispose();
                return decodeError;
            }
            
            // We can consider clip succesfully opened at this point
            State = State.Stopped;
            ClipOpened?.Invoke(this, new EventArgs());

            // Rebuild the shader if the defines have changed
            var requiredShaderDefines = ShaderDefinesForClip(clip);
            if ( GpuPipelineProgram == null || !requiredShaderDefines.ToHashSet().SetEquals(GpuPipelineProgram.Defines) )
            {
                if (GpuPipelineProgram != null)
                    GpuPipelineProgram.Dispose();
                GpuPipelineProgram = RenderContext.CreateShader(System.Reflection.Assembly.GetExecutingAssembly(), "PipelineCinemaDNG", "PipelineCinemaDNG", requiredShaderDefines);
            }

            // Create the sequence stream
            Debug.Assert(SequenceStream == null);
            SequenceStream = new SequenceStream<SequenceFrameDNG>((ClipCinemaDNG)clip, RenderContext, gpuFormat, bufferSizeFrames);

            // Allocate display frame
            displayFrameStaging = new byte[gpuFormat.BytesPerPixel() * clip.Metadata.Dimensions.Area()];

            // Create display texture with preview frame
            if (displayFrameGPU != null)
                displayFrameGPU.Dispose();
            displayFrameGPU = RenderContext.CreateTexture(cinemaDNGClip.Metadata.Dimensions, gpuFormat, cinemaDNGMetadata.TileCount == 0 ? previewFrame.decodedImage : null, 
                TextureFilter.Nearest, "displayFrame");

            // Tiled preview frame requires copying to GPU seperately
            Action discardPreviewFrame = () =>
            {
                previewFrame.Dispose();
                previewFrame = null;
            };
            if (cinemaDNGMetadata.TileCount > 0)
                previewFrame.CopyToGPU(clip, RenderContext, displayFrameGPU, null, false, discardPreviewFrame);
            else
                discardPreviewFrame();

            // Create linearization table texture
            if ( cinemaDNGMetadata.LinearizationTable != null && cinemaDNGMetadata.LinearizationTable.Length > 0 )
            {
                if (LinearizeTable != null)
                    LinearizeTable.Dispose();
                Span<byte> tableData = System.Runtime.InteropServices.MemoryMarshal.Cast<ushort, byte>(cinemaDNGMetadata.LinearizationTable);
                LinearizeTable = RenderContext.CreateTexture((uint)cinemaDNGMetadata.LinearizationTable.Length, GPU.Render.TextureFormat.R16, tableData.ToArray());
            }
            
            return Error.None;
        }

        public override void SeekStart()
        {
            base.SeekStart();

            // Do nothing if there is only 1 frame
            if (FirstFrame == LastFrame)
                return;

            // Allocate seek frame if it is not allocated
            if (SeekFrame == null)
                SeekFrame = new SequenceFrameDNG(RenderContext, Clip, displayFrameGPU.Format);

            // Decode seek frame processing
            Func<Error> decodeSeekFrame = () =>
            {
                if (!ActiveSeekRequest.HasValue)
                    return Error.None;

                try
                {
                    SeekFrameMutex.WaitOne();
                    SeekFrame.frameNumber = ActiveSeekRequest.Value;
                    SeekFrame.timeCode = null;
                    ActiveSeekRequest = null;
                    var decodeResult = SeekFrame.Decode(Clip);
#if SEEK_TRACE
                    if ( decodeResult == Error.None )
                        Trace.WriteLine("Decoded seek frame: " + SeekFrame.frameNumber);
#endif
                    PlayerWindow.InvokeOnUIThread(() => RenderContext.RequestRender());
                    return decodeResult;
                }
                finally
                {
                    SeekFrameMutex.ReleaseMutex();
                }
            };

            // Create new seek work
            Debug.Assert(SeekWork == null);
            if (SeekWork != null)
                SeekWork.Dispose();
            SeekWork = new Worker<Error>(decodeSeekFrame, true);
        }

        public override Error RequestSeek(uint frame, bool force = false)
        {
            Debug.Assert(SeekWork != null);

            // Don't try to seek if theres only 1 frame
            if (FirstFrame == LastFrame)
                return Error.None;

            base.RequestSeek(frame, force);
            if (SeekWork == null)
                return Error.FrameRequestError;

            // Nothing to do
            if (SeekFrame != null && SeekFrame.frameNumber == frame)
                return Error.None;

            // If forced, wait for previous work to finish
            if ( SeekWork.IsBusy && !force)
                return Error.SeekRequestAlreadyActive;
            else if (SeekWork.IsBusy && force)
                SeekWork.WaitForWork();

            Debug.Assert(!SeekWork.IsBusy);
            Debug.Assert(!ActiveSeekRequest.HasValue);
#if SEEK_TRACE
            Trace.WriteLine("Requesting seek frame: " + frame);
#endif
            // Set the new seek request
            ActiveSeekRequest = frame;
            if (SeekWork.IsSleeping)
                SeekWork.Resume();

            return Error.None;
        }

        public override void SeekEnd()
        {
            // Allow any final seeking work to complete and stop the worker
            if (SeekWork != null)
            {
                if (SeekWork.IsBusy)
                    SeekWork.WaitForWork();
                SeekWork.Stop(false);
                ActiveSeekRequest = null;
            }

            // Set the new next frame to be requested/displayed
            if (SeekFrame != null)
            {
                requestFrame = SeekFrame.frameNumber;
                displayFrame = SeekFrame.frameNumber;
            }

            base.SeekEnd();
            
            // Done with the seek work
            if (SeekWork != null)
            {
                SeekWork.Dispose();
                SeekWork = null;
            }
        }

        public override void Stop()
        {
            base.Stop();
            SequenceStream.CancelAllRequests();
            SequenceStream.ReclaimReadyFrames();
        }

        public override void Pause()
        {
            base.Pause();
            SequenceStream.CancelAllRequests();
            SequenceStream.ReclaimReadyFrames();
        }

        private IList<string> ShaderDefinesForClip(IClip clip)
        {
            Debug.Assert(SupportsClip(clip));
            var dngMetadata = (IO.DNG.MetadataCinemaDNG)clip.Metadata;
            Debug.Assert(dngMetadata != null);

            var defines = new List<string>();

            // Always output Rec709 for now
            defines.Add("GAMMA_REC709");

            switch (dngMetadata.CFAPattern)
            {
                case IO.CFAPattern.None:
                    defines.Add("MONOCHROME");
                    break;
                case IO.CFAPattern.RGGB:
                    defines.Add("BAYER_XGGX");
                    defines.Add("BAYER_RB");
                    break;
                case IO.CFAPattern.BGGR:
                    defines.Add("BAYER_XGGX");
                    defines.Add("BAYER_BR");
                    break;
                case IO.CFAPattern.GBRG:
                    defines.Add("BAYER_GXXG");
                    defines.Add("BAYER_BR");
                    break;
                case IO.CFAPattern.GRBG:
                    defines.Add("BAYER_GXXG");
                    defines.Add("BAYER_RB");
                    break;
                default:
                    throw new Exception("Unsupported DNG CFA pattern");
            }

            if ( dngMetadata.LinearizationTable != null && dngMetadata.LinearizationTable.Length > 0 )
                defines.Add("LINEARIZE");

            return defines;
        }

        public override bool SupportsClip(IClip clip)
        {
            return (clip.GetType() == typeof(ClipCinemaDNG) && SupportedEssence.Contains(clip.Essence));
        }

        public override void OnRenderFrame(double timeInterval)
        {
            if (GpuPipelineProgram != null && GpuPipelineProgram.Valid && displayFrameGPU != null && displayFrameGPU.Valid && Clip != null)
            {
                // Seek frame needs uploading to GPU
                if ( SeekFrame != null && SeekFrame.NeedsGPUCopy)
                {
                    try
                    {
                        SeekFrameMutex.WaitOne();
#if SEEK_TRACE
                        Trace.WriteLine("Copying seek frame: " + SeekFrame.frameNumber + " to GPU");
#endif
                        SeekFrame.CopyToGPU(Clip, RenderContext, displayFrameGPU, null, true);
                        if (!IsPlaying)
                        {
                            displayFrame = SeekFrame.frameNumber;
                            requestFrame = SeekFrame.frameNumber;
                            OnSeekFrameDisplay(SeekFrame.LastError, SeekFrame.timeCode);
                        }
                    }
                    finally
                    {
                        SeekFrameMutex.ReleaseMutex();
                    }
                }

                // Calculate and apply exposure
                var cinemaDNGMetadata = (IO.DNG.MetadataCinemaDNG)Clip.Metadata;
                var exposure = Math.Pow(2.0, Clip.RawParameters.Value.exposure.HasValue ? Clip.RawParameters.Value.exposure.Value : cinemaDNGMetadata.ExposureValue );
                GpuPipelineProgram.SetUniform(RenderContext, "exposure", (float)exposure);

                // Calculate and apply black/white levels
                var blackWhiteLevel = new Vector2(cinemaDNGMetadata.BlackLevel, cinemaDNGMetadata.WhiteLevel);
                var decodedMaxlevel = (1 << (int)Clip.Metadata.DecodedBitDepth) - 1;
                var linearMaxLevel = decodedMaxlevel;
                if (cinemaDNGMetadata.LinearizationTable != null && cinemaDNGMetadata.LinearizationTable.Length > 0 && LinearizeTable != null)
                    linearMaxLevel = (1 << ((int)LinearizeTable.Format.BytesPerPixel() * 8)) - 1;
                GpuPipelineProgram.SetUniform(RenderContext, "blackWhiteLevel", blackWhiteLevel / (float)linearMaxLevel);

                // Apply advanced raw parameters
                GpuPipelineProgram.SetUniform(RenderContext, "toneMappingOperator", (int)Clip.RawParameters.Value.toneMappingOperator.GetValueOrDefault(ToneMappingOperator.SDR));

                // Set linearization table range
                if (cinemaDNGMetadata.LinearizationTable != null && cinemaDNGMetadata.LinearizationTable.Length > 0 )
                {
                    var tableInputRange = (1 << (int)cinemaDNGMetadata.BitDepth) - 1;
                    GpuPipelineProgram.SetUniform(RenderContext, "linearizeTableRange", (float)tableInputRange / (float)decodedMaxlevel);
                }

                if ( Clip.Metadata.ColorProfile.HasValue )
                {
                    var colorProfile = Clip.Metadata.ColorProfile.Value;

                    // Combine camera to xyz/xyz to display colour matrices
                    var cameraToXYZD50Matrix = colorProfile.CalculateCameraToXYZD50(Clip.RawParameters.Value.whiteBalance);
                    var xyzToDisplayColourMatrix = Maths.Color.Matrix.XYZToRec709D50();
                    var cameraToDisplayColourMatrix = Maths.Color.Matrix.NormalizeColourMatrix(xyzToDisplayColourMatrix) * cameraToXYZD50Matrix;
                    GpuPipelineProgram.SetUniform(RenderContext, "cameraToDisplayColour", cameraToDisplayColourMatrix);

                    // Calculate camera white in RAW space
                    var cameraToDisplayInv = Matrix3.Invert(cameraToDisplayColourMatrix);
                    var whiteLevelCamera = cameraToDisplayInv * Vector3.One;
                    var cameraWhiteMin = Math.Min(Math.Min(whiteLevelCamera.X, whiteLevelCamera.Y), whiteLevelCamera.Z);
                    var cameraWhiteMax = Math.Max(Math.Max(whiteLevelCamera.X, whiteLevelCamera.Y), whiteLevelCamera.Z);
                    Vector3 cameraWhite = whiteLevelCamera / cameraWhiteMin;
                    Vector3 cameraWhiteNormalised = whiteLevelCamera / cameraWhiteMax;
                    GpuPipelineProgram.SetUniform(RenderContext, "cameraWhite", cameraWhite);

                    // Calculate luminance weights for RAW by pushing the standard rec709 luminance weights back through inverted CamreaTo709
                    // Used for highlight rolloff
                    var cameraTo709Inv = Matrix3.Invert(Maths.Color.Matrix.XYZToRec709D50() * cameraToXYZD50Matrix);
                    var luminanceWeightUnormalised = cameraTo709Inv * Maths.Color.Profile.Rec709LuminanceWeights;
                    Vector3 RAWLuminanceWeight = luminanceWeightUnormalised / (luminanceWeightUnormalised.X + luminanceWeightUnormalised.Y + luminanceWeightUnormalised.Z);
                    GpuPipelineProgram.SetUniform(RenderContext, "cameraWhiteNormalised", cameraWhiteNormalised);
                    GpuPipelineProgram.SetUniform(RenderContext, "rawLuminanceWeight", RAWLuminanceWeight);

                    // Apply advanced raw parameters
                    GpuPipelineProgram.SetUniform(RenderContext, "highlightRecovery", (int)Clip.RawParameters.Value.highlightRecovery.GetValueOrDefault(HighlightRecovery.On));
                    GpuPipelineProgram.SetUniform(RenderContext, "highlightRollOff", (int)Clip.RawParameters.Value.highlightRollOff.GetValueOrDefault(HighlightRollOff.Low));
                    GpuPipelineProgram.SetUniform(RenderContext, "gamutCompression",(int)Clip.RawParameters.Value.gamutCompression.GetValueOrDefault(GamutCompression.Rec709));
                }
                Vector2i rectPos;
                Vector2i rectSize;
                RenderContext.FramebufferSize.FitAspectRatio(Clip.Metadata.AspectRatio, out rectPos, out rectSize);
                var textures = new Dictionary<string, ITexture> { { "rawImage", displayFrameGPU } };
                if (LinearizeTable != null)
                    textures["linearizeTable"] = LinearizeTable;
                RenderContext.Draw2D(GpuPipelineProgram, textures, rectPos, rectSize);
            }
        }

        public override Error RequestFrame(uint frameNumber)
        {
            return SequenceStream.RequestFrame(frameNumber) == FrameRequestResult.Success ? Error.None : Error.FrameRequestError;
        }

        private uint FrameDistance(uint frame1, uint frame2)
        {
            return (uint)Math.Abs((int)frame1 - (int)frame2);
        }

        public override Error DisplayFrame(uint frameNumber, out uint actualFrameNumber, out TimeCode? actualTimeCode, PlaybackVelocity playbackVelocity)
        {
            actualFrameNumber = frameNumber;
            actualTimeCode = null;
            var ret = Error.None;

            // Attempt to get the frame for display
            var frame = SequenceStream.RetrieveFrame(frameNumber);

            // Frame not ready
            if ( frame == null )
            {
                // Find the nearest frame which is ready
                uint? nearestFrame = null;
                var readyFrames = SequenceStream.ReadyFrames();
                foreach(var readyFrame in readyFrames)
                {
                    if (playbackVelocity.IsForward() && readyFrame > frameNumber)
                        continue;
                    if (!playbackVelocity.IsForward() && readyFrame < frameNumber)
                        continue;
                    if (!nearestFrame.HasValue || FrameDistance(frameNumber, readyFrame) < FrameDistance(frameNumber, nearestFrame.Value))
                        nearestFrame = readyFrame;
                }

                // Use the nearest frame
                if (nearestFrame.HasValue)
                    frame = SequenceStream.RetrieveFrame(nearestFrame.Value);
                ret = Error.FrameNotReady;
            }

            // We got a frame, 'display' it
            if (frame != null)
            {
                if (displayFrameGPU != null)
                    frame.CopyToGPU(Clip, RenderContext, displayFrameGPU, displayFrameStaging);
                RenderContext.RequestRender();
                actualFrameNumber = frame.frameNumber;
                actualTimeCode = frame.timeCode;
                if (frame.LastError == Error.FrameNotPresent)
                    ret = Error.FrameNotPresent;
            }
            else
                ret = Error.FrameNotReady;

            // Play direction is forward, reclaim or cancel any frames up to the intended display frame
            if (playbackVelocity.IsForward()) 
            {
                SequenceStream.ReclaimReadyFramesUpTo(frameNumber);
                SequenceStream.CancelRequestsUpTo(frameNumber);
            } 
            else
            {
                SequenceStream.ReclaimReadyFramesFrom(frameNumber);
                SequenceStream.CancelRequestsFrom(frameNumber);
            }

            return ret;
        }
    }
}

