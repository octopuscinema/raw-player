using System;
namespace Octopus.Player.Core
{
	public enum Error
    {
        None,
        ClipNotValidated,
        BadPath,
        BadFile,
        BadMetadata,
        NoVideoStream,
        FrameNotPresent,
        BadFrame,
        BadImageData,
        BadFrameIndex,
        FrameNotReady,
        NotImplmeneted
    }
}