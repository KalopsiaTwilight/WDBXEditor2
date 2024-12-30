using System;
using System.IO;

namespace WDBXEditor2.Misc
{
    public interface ISettingsStorage
    {
        public void Store(string key, string value);
        public string Get(string key);
        public void Remove(string key);
    }

    public class SettingStorage: ISettingsStorage
    {
        protected JsonSettings settings = null;

        public void Initialize()
        {
            settings = new JsonSettings(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                AppDomain.CurrentDomain.FriendlyName + ".json"
            ));
        }

        public void Store(string key, string value)
        {
            settings[key] = value;
            settings.Save();
        }

        public string Get(string key)
        {
            return settings[key];
        }

        public void Remove(string key)
        {
            settings.RemoveSetting(key);
            settings.Save();
        }

        public void Save()
        {
            settings.Save();
        }
    }
}
