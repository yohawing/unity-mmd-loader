#nullable enable

namespace Mmd.Native
{
    internal sealed partial class MmdRuntimeFfiPlaybackSession
    {
        internal int GetBoneTrackCount()
        {
            ThrowIfDisposed();
            return MmdRuntimeFfiTrackReadback.GetClipBoneTrackCount(clip);
        }

        internal MmdRuntimeFfiMethods.BoneTrackDescriptor GetBoneTrackDescriptor(int trackIndex)
        {
            ThrowIfDisposed();
            return MmdRuntimeFfiTrackReadback.GetClipBoneTrackDescriptor(clip, trackIndex);
        }

        internal MmdRuntimeFfiMethods.BoneTrackKey[] GetBoneTrackKeys(int trackIndex)
        {
            ThrowIfDisposed();
            return MmdRuntimeFfiTrackReadback.CopyClipBoneTrackKeys(clip, trackIndex);
        }

        internal int GetMorphTrackCount()
        {
            ThrowIfDisposed();
            return MmdRuntimeFfiTrackReadback.GetClipMorphTrackCount(clip);
        }

        internal MmdRuntimeFfiMethods.MorphTrackDescriptor GetMorphTrackDescriptor(int trackIndex)
        {
            ThrowIfDisposed();
            return MmdRuntimeFfiTrackReadback.GetClipMorphTrackDescriptor(clip, trackIndex);
        }

        internal MmdRuntimeFfiMethods.MorphTrackKey[] GetMorphTrackKeys(int trackIndex)
        {
            ThrowIfDisposed();
            return MmdRuntimeFfiTrackReadback.CopyClipMorphTrackKeys(clip, trackIndex);
        }

        internal int GetPropertyTrackCount()
        {
            ThrowIfDisposed();
            return MmdRuntimeFfiTrackReadback.GetClipPropertyTrackCount(clip);
        }

        internal MmdRuntimeFfiMethods.PropertyTrackDescriptor GetPropertyTrackDescriptor()
        {
            ThrowIfDisposed();
            return MmdRuntimeFfiTrackReadback.GetClipPropertyTrackDescriptor(clip);
        }

        internal MmdRuntimeFfiMethods.PropertyTrackKey[] GetPropertyTrackKeys()
        {
            ThrowIfDisposed();
            return MmdRuntimeFfiTrackReadback.CopyClipPropertyTrackKeys(clip);
        }

        internal byte[] GetPropertyTrackIkEnabled()
        {
            ThrowIfDisposed();
            return MmdRuntimeFfiTrackReadback.CopyClipPropertyTrackIkEnabled(clip);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new System.ObjectDisposedException(nameof(MmdRuntimeFfiPlaybackSession));
            }
        }
    }
}
