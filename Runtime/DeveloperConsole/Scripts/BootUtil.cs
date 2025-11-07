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
}