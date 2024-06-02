using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Octopus.Player.Core.IO.LUT;
using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Compute;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.Playback
{
    public class PlaybackCinemaDNG : Playback
    {
        private static readonly uint nativeMemoryBufferSize = 0;
        private static readonly uint bufferDurationFrames = 6;
        private static readonly uint bufferSizeFrames = 12;
        private static readonly List<string> pipelineKernels = new List<string> { "ProcessBayer", "ProcessBayerLUT", "Process", "ProcessLUT" };

        private Worker<Error> SeekWork { get; set; }
        private SequenceFrameDNG SeekFrame { get; set; }
        private Mutex SeekFrameMutex { get; set; }

        private ISequenceStream SequenceStream { get; set; }
        private IProgram GpuPipelineComputeProgram { get; set; }

        private IImage1D LinearizeTable { get; set; }

        private IO.LUT.LUT3D LUT { get; set; }

        public override event EventHandler ClipOpened;
        public override event EventHandler ClipClosed;

        public override List<Essence> SupportedEssence { get { return new List<Essence>() { Essence.Sequence }; } }

        public override uint FirstFrame { get { return ((IO.DNG.MetadataCinemaDNG)Clip.Metadata).FirstFrame; } }
        public override uint LastFrame { get { return ((IO.DNG.MetadataCinemaDNG)Clip.Metadata).LastFrame; } }

        public override uint? ActiveSeekRequest { get; protected set; }

        byte[] displayFrameStaging;
        ITexture displayFrameGPU;
        IImage2D displayFrameCompute;

        public PlaybackCinemaDNG(IPlayerWindow playerWindow, GPU.Compute.IContext computeContext, GPU.Render.IContext renderContext)
            : base(playerWindow, computeContext, renderContext, bufferDurationFrames)
        {
            SeekFrameMutex = new Mutex();

            PlayerWindow.RawParameterChanged += OnRawParameterChanged;
        }

        public override void Dispose()
        {
            PlayerWindow.RawParameterChanged -= OnRawParameterChanged;
            
            LUT?.Dispose();
            LUT = null;

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
            var gpuFormat = clip.Metadata.DecodedBitDepth <= 8 ? GPU.Format.R8 : GPU.Format.R16;
            var gpuDisplayFormat = GPU.Format.RGBX8;
            var previewFrame = new SequenceFrameDNG(ComputeContext, ComputeContext.DefaultQueue, clip, gpuFormat);
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

            // Rebuild the compute program if the defines have changed
            var requiredGpuDefines = GpuDefinesForClip(clip);
            if (GpuPipelineComputeProgram == null || !requiredGpuDefines.ToHashSet().SetEquals(GpuPipelineComputeProgram.Defines))
            {
                if (GpuPipelineComputeProgram != null)
                    GpuPipelineComputeProgram.Dispose();
                GpuPipelineComputeProgram = ComputeContext.CreateProgram(Assembly.GetExecutingAssembly(), "PipelineCinemaDNG", pipelineKernels, requiredGpuDefines, "PipelineCinemaDNG");
            }

            // Create the sequence stream
            Debug.Assert(SequenceStream == null);
            SequenceStream = new SequenceStream<SequenceFrameDNG>(ComputeContext, (ClipCinemaDNG)clip, gpuFormat, bufferSizeFrames, nativeMemoryBufferSize);

            // Create linearization table texture
            if (cinemaDNGMetadata.LinearizationTable != null && cinemaDNGMetadata.LinearizationTable.Length > 0)
            {
                if (LinearizeTable != null)
                    LinearizeTable.Dispose();
                Span<byte> tableData = System.Runtime.InteropServices.MemoryMarshal.Cast<ushort, byte>(cinemaDNGMetadata.LinearizationTable);

                LinearizeTable = ComputeContext.CreateImage(cinemaDNGMetadata.LinearizationTable.Length, GPU.Format.R16, MemoryDeviceAccess.ReadOnly,
                    MemoryHostAccess.WriteOnly, tableData.ToArray());
            }

            // Create display frame compute and render image
            if (displayFrameGPU != null)
                displayFrameGPU.Dispose();
            displayFrameGPU = RenderContext.CreateTexture(cinemaDNGClip.Metadata.PaddedDimensions, gpuDisplayFormat, null, TextureFilter.Linear, "displayFrame");
            if (displayFrameCompute != null)
                displayFrameCompute.Dispose();
            displayFrameCompute = ComputeContext.CreateImage(RenderContext, displayFrameGPU, MemoryDeviceAccess.ReadWrite);

            // Process preview frame, discarding frame afterwards
            Action discardPreviewFrame = () =>
            {
                previewFrame.Dispose();
                previewFrame = null;
            };
            previewFrame.Process(clip, RenderContext, displayFrameCompute, LinearizeTable, GpuPipelineComputeProgram, ComputeContext.DefaultQueue,
               LUT, false, discardPreviewFrame);

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
                SeekFrame = new SequenceFrameDNG(ComputeContext, ComputeContext.DefaultQueue, Clip, SequenceStream.Format);

            // Decode seek frame processing
            Func<byte[],Error> decodeSeekFrame = (byte[] workingBuffer) =>
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

        public void OnRawParameterChanged()
        {
            if (IsPlaying || State == State.Empty || !IsOpen())
                return;

            // If raw parameters changed while not playing, update the paused frame
            Action updatePausedFrame = () =>
            {
                if (!IsPlaying && State != State.Empty && IsOpen())
                {
                    // TODO: maybe cache this frame as 'pauseFrame' or something
                    var frame = new SequenceFrameDNG(ComputeContext, ComputeContext.DefaultQueue, Clip, SequenceStream.Format);
                    frame.frameNumber = LastDisplayedFrame.Value;// displayFrame.HasValue ? (uint)displayFrame.Value : FirstFrame;
                    frame.Decode(Clip);

                    Action discardFrame = () =>
                    {
                        frame.Dispose();
                        frame = null;
                    };

                    frame.Process(Clip, RenderContext, displayFrameCompute, LinearizeTable, GpuPipelineComputeProgram, ComputeContext.DefaultQueue, LUT, true, discardFrame);
                }
            };

            RenderContext.EnqueueRenderAction(updatePausedFrame);
        }

        private IReadOnlyCollection<string> GpuDefinesForClip(IClip clip)
        {
            Debug.Assert(SupportsClip(clip));
            var dngMetadata = (IO.DNG.MetadataCinemaDNG)clip.Metadata;
            Debug.Assert(dngMetadata != null);

            var defines = new List<string>();

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

            if (dngMetadata.LinearizationTable != null && dngMetadata.LinearizationTable.Length > 0)
                defines.Add("LINEARIZE");

            return defines;
        }

        public override bool SupportsClip(IClip clip)
        {
            return (clip.GetType() == typeof(ClipCinemaDNG) && SupportedEssence.Contains(clip.Essence));
        }

        public override void OnRenderFrame(double timeInterval)
        {
            if ( GpuPipelineComputeProgram != null && displayFrameGPU != null && displayFrameGPU.Valid && displayFrameCompute != null && displayFrameCompute.Valid && Clip != null)
            {
                // Seek frame needs processing
                if (SeekFrame != null && !SeekFrame.Processed)
                {
                    try
                    {
                        SeekFrameMutex.WaitOne();
#if SEEK_TRACE
                        Trace.WriteLine("Processing seek frame: " + SeekFrame.frameNumber + " on GPU");
#endif
                        SeekFrame.Process(Clip, RenderContext, displayFrameCompute, LinearizeTable, GpuPipelineComputeProgram, ComputeContext.DefaultQueue, LUT);
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

                Vector2i rectPos;
                Vector2i rectSize;
                RenderContext.FramebufferSize.FitAspectRatio(Clip.Metadata.AspectRatio, out rectPos, out rectSize);
                RenderContext.Blit2D(displayFrameGPU, rectPos, rectSize);
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

            // We got a frame, run it through the GPU
            if (frame != null)
            {
                if (displayFrameCompute != null && displayFrameCompute.Valid && displayFrameGPU != null)
                    ((SequenceFrameRAW)frame).Process(Clip, RenderContext, displayFrameCompute, LinearizeTable, GpuPipelineComputeProgram, ComputeContext.DefaultQueue, LUT);

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

        public override void RemoveLUT()
        {
            if ( LUT != null )
            {
                Action removeLut = () =>
                { 
                    LUT?.Dispose();
                    LUT = null;
                };
                RenderContext.EnqueueRenderAction(removeLut);
            }
        }

        public override Error ApplyLUT(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames();
            foreach (string resource in resources)
            {
                if (resource.Contains(resourceName))
                {
                    try
                    {
                        var lut = new LUT3D(ComputeContext, assembly, resource);
                        Action changeLut = () =>
                        {
                            LUT?.Dispose();
                            LUT = lut;
                        };
                        RenderContext.EnqueueRenderAction(changeLut);
                        return Error.None;
                    }
                    catch
                    {
                        return Error.InvalidLutFile;
                    }
                }
            }

            return Error.LutNotFound;
        }

        public override Error ApplyLUT(Uri path)
        {
            if (!File.Exists(path.OriginalString))
                return Error.LutNotFound;

            try
            {
                var lut = new LUT3D(ComputeContext, path.OriginalString);
                Action changeLut = () =>
                {
                    LUT?.Dispose();
                    LUT = lut;
                };
                RenderContext.EnqueueRenderAction(changeLut);
                return Error.None;
            }
            catch
            {
                return Error.InvalidLutFile;
            }
        }
    }
}

