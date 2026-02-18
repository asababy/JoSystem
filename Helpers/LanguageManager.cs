using System;
using System.Linq;
using System.Windows;

namespace JoSystem.Helpers
{
    public static class LanguageManager
    {
        public static event Action<string> LanguageChanged;

        public static void SetLanguage(string cultureCode)
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/JoSystem;component/Assets/Languages/{cultureCode}.xaml")
            };

            // Remove old language dictionaries
            var oldDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Assets/Languages/"));

            if (oldDict != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(oldDict);
            }

            // Add new dictionary
            Application.Current.Resources.MergedDictionaries.Add(dict);
            
            LanguageChanged?.Invoke(cultureCode);
        }

        public static string GetString(string key)
        {
            if (Application.Current.TryFindResource(key) is string value)
            {
                return value;
            }
            return key;
        }
    }
}
