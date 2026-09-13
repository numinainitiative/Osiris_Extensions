using System.Windows.Media;

namespace Osiris.Extensions.Exophase
{
    internal sealed class ExophasePlatformVisual
    {
        public string DisplayName { get; set; }

        public string ColorHex { get; set; }

        public Brush Brush { get; set; }

        public Geometry IconGeometry { get; set; }

        public bool UseOsirisLogo { get; set; }
    }

    internal static class ExophasePlatformVisualCatalog
    {
        // Brand vector paths are sourced from Simple Icons under CC0-1.0.
        // Steam, PlayStation, Android and Apple: simple-icons commit
        // b054428646591252023b9599defb56f6e0b32f10.
        // Xbox, Windows, Nintendo Switch and EA: simple-icons 8.0.0.
        private const string SteamPath =
            "M11.979 0C5.678 0 .511 4.86.022 11.037l6.432 2.658c.545-.371 1.203-.59 1.912-.59.063 0 .125.004.188.006l2.861-4.142V8.91c0-2.495 2.028-4.524 4.524-4.524 2.494 0 4.524 2.031 4.524 4.527s-2.03 4.525-4.524 4.525h-.105l-4.076 2.911c0 .052.004.105.004.159 0 1.875-1.515 3.396-3.39 3.396-1.635 0-3.016-1.173-3.331-2.727L.436 15.27C1.862 20.307 6.486 24 11.979 24c6.627 0 11.999-5.373 11.999-12S18.605 0 11.979 0zM7.54 18.21l-1.473-.61c.262.543.714.999 1.314 1.25 1.297.539 2.793-.076 3.332-1.375.263-.63.264-1.319.005-1.949s-.75-1.121-1.377-1.383c-.624-.26-1.29-.249-1.878-.03l1.523.63c.956.4 1.409 1.5 1.009 2.455-.397.957-1.497 1.41-2.454 1.012H7.54zm11.415-9.303c0-1.662-1.353-3.015-3.015-3.015-1.665 0-3.015 1.353-3.015 3.015 0 1.665 1.35 3.015 3.015 3.015 1.663 0 3.015-1.35 3.015-3.015zm-5.273-.005c0-1.252 1.013-2.266 2.265-2.266 1.249 0 2.266 1.014 2.266 2.266 0 1.251-1.017 2.265-2.266 2.265-1.253 0-2.265-1.014-2.265-2.265z";

        private const string PlayStationPath =
            "M8.984 2.596v17.547l3.915 1.261V6.688c0-.69.304-1.151.794-.991.636.18.76.814.76 1.505v5.875c2.441 1.193 4.362-.002 4.362-3.152 0-3.237-1.126-4.675-4.438-5.827-1.307-.448-3.728-1.186-5.39-1.502zm4.656 16.241l6.296-2.275c.715-.258.826-.625.246-.818-.586-.192-1.637-.139-2.357.123l-4.205 1.5V14.98l.24-.085s1.201-.42 2.913-.615c1.696-.18 3.785.03 5.437.661 1.848.601 2.04 1.472 1.576 2.072-.465.6-1.622 1.036-1.622 1.036l-8.544 3.107V18.86zM1.807 18.6c-1.9-.545-2.214-1.668-1.352-2.32.801-.586 2.16-1.052 2.16-1.052l5.615-2.013v2.313L4.205 17c-.705.271-.825.632-.239.826.586.195 1.637.15 2.343-.12L8.247 17v2.074c-.12.03-.256.044-.39.073-1.939.331-3.996.196-6.038-.479z";

        private const string XboxPath =
            "M4.102 21.033C6.211 22.881 8.977 24 12 24c3.026 0 5.789-1.119 7.902-2.967 1.877-1.912-4.316-8.709-7.902-11.417-3.582 2.708-9.779 9.505-7.898 11.417zm11.16-14.406c2.5 2.961 7.484 10.313 6.076 12.912C23.002 17.48 24 14.861 24 12.004c0-3.34-1.365-6.362-3.57-8.536 0 0-.027-.022-.082-.042-.063-.022-.152-.045-.281-.045-.592 0-1.985.434-4.805 3.246zM3.654 3.426c-.057.02-.082.041-.086.042C1.365 5.642 0 8.664 0 12.004c0 2.854.998 5.473 2.661 7.533-1.401-2.605 3.579-9.951 6.08-12.91-2.82-2.813-4.216-3.245-4.806-3.245-.131 0-.223.021-.281.046v-.002zM12 3.551S9.055 1.828 6.755 1.746c-.903-.033-1.454.295-1.521.339C7.379.646 9.659 0 11.984 0H12c2.334 0 4.605.646 6.766 2.085-.068-.046-.615-.372-1.52-.339C14.946 1.828 12 3.545 12 3.545v.006z";

        private const string WindowsPath =
            "M0 3.449L9.75 2.1v9.451H0m10.949-9.602L24 0v11.4H10.949M0 12.6h9.75v9.451L0 20.699m10.949-8.099H24V24l-13.051-1.851";

        private const string NintendoSwitchPath =
            "M14.176 24h3.674C21.243 24 24 21.24 24 17.846V6.15C24 2.76 21.243 0 17.85 0h-3.674a.433.433 0 0 0-.434.434v23.132c0 .24.194.434.434.434zm3.704-13.551a1.51 1.51 0 1 1 0 3.022 1.51 1.51 0 0 1 0-3.022zM11.178 0H6.149A6.157 6.157 0 0 0 0 6.15v11.696A6.157 6.157 0 0 0 6.15 24h5.028a.433.433 0 0 0 .434-.434V.434A.433.433 0 0 0 11.178 0zM9.09 21.39H6.15a3.548 3.548 0 0 1-3.544-3.544V6.15A3.548 3.548 0 0 1 6.15 2.607h2.94V21.39zM4.89 6.854a1.51 1.51 0 1 0 3.02 0 1.51 1.51 0 0 0-3.02 0z";

        private const string ElectronicArtsPath =
            "M16.635 6.162l-5.928 9.377H4.24l1.508-2.3h4.024l1.474-2.335H2.264L.79 13.239h2.156L0 17.84h12.072l4.563-7.259 1.652 2.66h-1.401l-1.473 2.299h4.347l1.473 2.3H24zm-11.461.107L3.7 8.604l9.52-.035 1.474-2.3z";

        private const string AndroidPath =
            "M18.4395 5.5586c-.675 1.1664-1.352 2.3318-2.0274 3.498-.0366-.0155-.0742-.0286-.1113-.043-1.8249-.6957-3.484-.8-4.42-.787-1.8551.0185-3.3544.4643-4.2597.8203-.084-.1494-1.7526-3.021-2.0215-3.4864a1.1451 1.1451 0 0 0-.1406-.1914c-.3312-.364-.9054-.4859-1.379-.203-.475.282-.7136.9361-.3886 1.5019 1.9466 3.3696-.0966-.2158 1.9473 3.3593.0172.031-.4946.2642-1.3926 1.0177C2.8987 12.176.452 14.772 0 18.9902h24c-.119-1.1108-.3686-2.099-.7461-3.0683-.7438-1.9118-1.8435-3.2928-2.7402-4.1836a12.1048 12.1048 0 0 0-2.1309-1.6875c.6594-1.122 1.312-2.2559 1.9649-3.3848.2077-.3615.1886-.7956-.0079-1.1191a1.1001 1.1001 0 0 0-.8515-.5332c-.5225-.0536-.9392.3128-1.0488.5449zm-.0391 8.461c.3944.5926.324 1.3306-.1563 1.6503-.4799.3197-1.188.0985-1.582-.4941-.3944-.5927-.324-1.3307.1563-1.6504.4727-.315 1.1812-.1086 1.582.4941zM7.207 13.5273c.4803.3197.5506 1.0577.1563 1.6504-.394.5926-1.1038.8138-1.584.4941-.48-.3197-.5503-1.0577-.1563-1.6504.4008-.6021 1.1087-.8106 1.584-.4941z";

        private const string ApplePath =
            "M12.152 6.896c-.948 0-2.415-1.078-3.96-1.04-2.04.027-3.91 1.183-4.961 3.014-2.117 3.675-.546 9.103 1.519 12.09 1.013 1.454 2.208 3.09 3.792 3.039 1.52-.065 2.09-.987 3.935-.987 1.831 0 2.35.987 3.96.948 1.637-.026 2.676-1.48 3.676-2.948 1.156-1.688 1.636-3.325 1.662-3.415-.039-.013-3.182-1.221-3.22-4.857-.026-3.04 2.48-4.494 2.597-4.559-1.429-2.09-3.623-2.324-4.39-2.376-2-.156-3.675 1.09-4.61 1.09zM15.53 3.83c.843-1.012 1.4-2.427 1.245-3.83-1.207.052-2.662.805-3.532 1.818-.78.896-1.454 2.338-1.273 3.714 1.338.104 2.715-.688 3.559-1.701";

        private const string GenericPath =
            "M2 2h9v9H2zM13 2h9v9h-9zM2 13h9v9H2zM13 13h9v9h-9z";

        internal static ExophasePlatformVisual Resolve(string platform)
        {
            var source = platform?.Trim() ?? string.Empty;
            var value = source.ToLowerInvariant();
            if (value == "osiris")
            {
                return CreateOsiris();
            }

            if (value.Contains("steam"))
            {
                return Create("Steam", "#66C0F4", SteamPath);
            }

            if (value == "ps3" || value.Contains("playstation 3"))
            {
                return Create("PlayStation 3", "#0070D1", PlayStationPath);
            }

            if (value == "ps4" || value.Contains("playstation 4"))
            {
                return Create("PlayStation 4", "#0070D1", PlayStationPath);
            }

            if (value == "ps5" || value.Contains("playstation 5"))
            {
                return Create("PlayStation 5", "#0070D1", PlayStationPath);
            }

            if (value.Contains("playstation") || value == "psn" || value.StartsWith("ps"))
            {
                return Create("PlayStation", "#0070D1", PlayStationPath);
            }

            if (value.Contains("xbox"))
            {
                var display = value.Contains("360") ? "Xbox 360" : "Xbox";
                return Create(display, "#107C10", XboxPath);
            }

            if (value.Contains("electronic arts") || value == "ea" || value.Contains("origin"))
            {
                return Create("EA", "#FF4747", ElectronicArtsPath);
            }

            if (value.Contains("windows"))
            {
                return Create("Windows", "#00A4EF", WindowsPath);
            }

            if (value == "switch" || value.Contains("nintendo switch"))
            {
                return Create("Nintendo Switch", "#E60012", NintendoSwitchPath);
            }

            if (value.Contains("android") || value.Contains("google play"))
            {
                return Create("Android", "#3DDC84", AndroidPath);
            }

            if (value == "ios" || value.Contains("apple"))
            {
                return Create("iOS", "#A2AAAD", ApplePath);
            }

            return Create(
                string.IsNullOrWhiteSpace(source) ? "Unknown" : source,
                "#318DB4",
                GenericPath);
        }

        private static ExophasePlatformVisual Create(
            string displayName,
            string colorHex,
            string iconPath)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            brush.Freeze();
            var geometry = Geometry.Parse(iconPath);
            geometry.Freeze();
            return new ExophasePlatformVisual
            {
                DisplayName = displayName,
                ColorHex = colorHex,
                Brush = brush,
                IconGeometry = geometry
            };
        }

        private static ExophasePlatformVisual CreateOsiris()
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F1F7"));
            brush.Freeze();
            return new ExophasePlatformVisual
            {
                DisplayName = "Osiris",
                ColorHex = "#F0F1F7",
                Brush = brush,
                IconGeometry = null,
                UseOsirisLogo = true
            };
        }
    }

}
