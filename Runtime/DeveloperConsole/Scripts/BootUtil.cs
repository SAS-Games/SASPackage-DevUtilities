using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    public static class BoolUtil
    {
        public static bool TryParse(string value, out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToLowerInvariant())
            {
                case "true":
                case "on":
                case "1":
                case "yes":
                case "enable":
                    result = true;
                    return true;

                case "false":
                case "off":
                case "0":
                case "no":
                case "disable":
                    result = false;
                    return true;

                default:
                    return false;
            }
        }
    }

    public static class VectorParseUtil
    {
        public static bool TryParseVector3(string x, string y, string z, out Vector3 result)
        {
            result = default;

            if (!float.TryParse(x, out float fx)) return false;
            if (!float.TryParse(y, out float fy)) return false;
            if (!float.TryParse(z, out float fz)) return false;

            result = new Vector3(fx, fy, fz);
            return true;
        }

        public static bool TryParseVector2(string x, string y, out Vector2 result)
        {
            result = default;

            if (!float.TryParse(x, out float fx)) return false;
            if (!float.TryParse(y, out float fy)) return false;

            result = new Vector2(fx, fy);
            return true;
        }
    }
}