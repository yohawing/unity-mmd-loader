#nullable enable

using System;
using UnityEngine;

namespace Yohawing.MmdUnity.UnityIntegration
{
    /// <summary>
    /// Built-in MMD shared toon ramp textures (toon01-toon10), embedded so that
    /// PMX materials referencing a shared toon index render with the real MikuMikuDance
    /// shade ramp instead of falling back to flat lighting. The ramps are derived from the
    /// genuine MikuMikuDance 9.32 distribution toon01.bmp-toon10.bmp (the GoldenOracle
    /// source). MMD toon ramps carry no horizontal detail and this shader always samples
    /// the toon at U=0.5, so each ramp is stored as a 1x32 vertical strip (the U=0.5
    /// bilinear tap of the original 32x32) - byte-identical sampling at a fraction of the size.
    /// </summary>
    internal static class MmdSharedToonTextures
    {
        /// <summary>Number of MMD shared toon ramps (PMX shared toon index 0-9).</summary>
        public const int SharedToonCount = 10;

        /// <summary>True when <paramref name="sharedToonIndex"/> is a valid 0-based shared toon index.</summary>
        public static bool IsSharedToonIndex(int sharedToonIndex)
        {
            return sharedToonIndex >= 0 && sharedToonIndex < SharedToonCount;
        }

        /// <summary>
        /// Decode a fresh readable <see cref="Texture2D"/> (1x32 vertical ramp) for the given
        /// 0-based shared toon index (0 =&gt; toon01). Returns null when the index is out of
        /// range. The caller owns the returned texture and is responsible for its hideFlags / lifetime.
        /// </summary>
        public static Texture2D? TryCreateSharedToonTexture(int sharedToonIndex)
        {
            if (!IsSharedToonIndex(sharedToonIndex))
            {
                return null;
            }

            byte[] bytes = Convert.FromBase64String(EncodedToonBmp[sharedToonIndex]);
            return MmdBmpDecoder.Decode(bytes, $"MMD Shared Toon {sharedToonIndex + 1:00}");
        }

        // Real MikuMikuDance 9.32 toon01..toon10 reduced to 1x32 24bpp BMP (U=0.5 tap), base64.
        private static readonly string[] EncodedToonBmp =
        {
            // toon01
            "Qk22AAAAAAAAADYAAAAoAAAAAQAAACAAAAABABgAAAAAAIAAAAATCwAAEwsAAAAAAAAAAAAAzc3NAM3NzQDNzc0Azc3NAM3NzQDNzc0Azc3NAM3NzQDNzc0Azc3NAM3NzQDNzc0Azc3NAM3NzQDNzc0Azc3NAP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wA=",
            // toon02
            "Qk22AAAAAAAAADYAAAAoAAAAAQAAACAAAAABABgAAAAAAIAAAAATCwAAEwsAAAAAAAAAAAAA4eH1AOHh9QDh4fUA4eH1AOHh9QDh4fUA4eH1AOHh9QDh4fUA4eH1AOHh9QDh4fUA4eH1AOHh9QDh4fUA4eH1AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wA=",
            // toon03
            "Qk22AAAAAAAAADYAAAAoAAAAAQAAACAAAAABABgAAAAAAIAAAAATCwAAEwsAAAAAAAAAAAAAmpqaAJqamgCampoAmpqaAJqamgCampoAmpqaAJqamgCampoAmpqaAJqamgCampoAmpqaAJqamgCampoAmpqaAP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wA=",
            // toon04
            "Qk22AAAAAAAAADYAAAAoAAAAAQAAACAAAAABABgAAAAAAIAAAAATCwAAEwsAAAAAAAAAAAAA6+/4AOvv+ADr7/gA6+/4AOvv+ADr7/gA6+/4AOvv+ADr7/gA6+/4AOvv+ADr7/gA6+/4AOvv+ADr7/gA6+/4AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wA=",
            // toon05
            "Qk22AAAAAAAAADYAAAAoAAAAAQAAACAAAAABABgAAAAAAIAAAAATCwAAEwsAAAAAAAAAAAAA3uf+AN3n/wDd5/8A3eb/AN3n/wDd5/8A3uf/AN/o/gDk7P8A6vD/APD0/wD2+P8A+/z/AP7+/wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wA=",
            // toon06
            "Qk22AAAAAAAAADYAAAAoAAAAAQAAACAAAAABABgAAAAAAIAAAAATCwAAEwsAAAAAAAAAAAAAA6zDAAOswwADrMMAA6zDAAOswwADrMMABK3EAAyyyQAyzOEAWOf6AGHt/wBh7f8AYe3/AGHt/wBh7f8AYe3/AGHt/wBh7f8AYe3/AGHt/wBn7v8Ay/r/ANX6/wCZ9P8AY+7/AGHt/wBh7f8AYe3/AGHt/wBh7f8AYe3/AGHt/wA=",
            // toon07
            "Qk22AAAAAAAAADYAAAAoAAAAAQAAACAAAAABABgAAAAAAIAAAAATCwAAEwsAAAAAAAAAAAAA////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wA=",
            // toon08
            "Qk22AAAAAAAAADYAAAAoAAAAAQAAACAAAAABABgAAAAAAIAAAAATCwAAEwsAAAAAAAAAAAAA////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wA=",
            // toon09
            "Qk22AAAAAAAAADYAAAAoAAAAAQAAACAAAAABABgAAAAAAIAAAAATCwAAEwsAAAAAAAAAAAAA////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wA=",
            // toon10
            "Qk22AAAAAAAAADYAAAAoAAAAAQAAACAAAAABABgAAAAAAIAAAAATCwAAEwsAAAAAAAAAAAAA////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wD///8A////AP///wA=",
        };
    }
}
