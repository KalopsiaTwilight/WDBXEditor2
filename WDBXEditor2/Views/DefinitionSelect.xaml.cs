﻿using DBCD;
using DBDefsLib;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WDBXEditor2.Misc;
using static DBDefsLib.Structs;

namespace WDBXEditor2.Views
{
    public partial class DefinitionSelect : Window
    {
        private List<DefinitionSelectData> definitionSelectData = new List<DefinitionSelectData>();
        private List<LocaleSelectInfo> localeSelectData = new List<LocaleSelectInfo>()
        {
            new LocaleSelectInfo() { DisplayName = "enUS", Locale = Locale.EnUS },
            new LocaleSelectInfo() { DisplayName = "enGB", Locale = Locale.EnGB },
            new LocaleSelectInfo() { DisplayName = "koKR", Locale = Locale.KoKR },
            new LocaleSelectInfo() { DisplayName = "frFR", Locale = Locale.FrFR },
            new LocaleSelectInfo() { DisplayName = "deDE", Locale = Locale.DeDE },
            new LocaleSelectInfo() { DisplayName = "enCN", Locale = Locale.EnCN },
            new LocaleSelectInfo() { DisplayName = "zhCN", Locale = Locale.ZhCN },
            new LocaleSelectInfo() { DisplayName = "enTW", Locale = Locale.EnTW },
            new LocaleSelectInfo() { DisplayName = "zhTW", Locale = Locale.ZhTW },
            new LocaleSelectInfo() { DisplayName = "esES", Locale = Locale.EsES },
            new LocaleSelectInfo() { DisplayName = "esMX", Locale = Locale.EsMX },
            new LocaleSelectInfo() { DisplayName = "ruRU", Locale = Locale.RuRU },
            new LocaleSelectInfo() { DisplayName = "ptPT", Locale = Locale.PtPT },
            new LocaleSelectInfo() { DisplayName = "ptBR", Locale = Locale.PtBR },
            new LocaleSelectInfo() { DisplayName = "itIT", Locale = Locale.ItIT }
        };

        public bool IsCanceled = false;
        public string SelectedVersion = null;
        public Locale SelectedLocale = Locale.EnUS;

        private readonly ISettingsStorage _settings;

        public DefinitionSelect(ISettingsStorage settings)
        {
            InitializeComponent();

            _settings = settings;

            DefinitionSelectList.Focus();
            DefinitionSelectList.ItemsSource = definitionSelectData;
            LocaleSelectList.ItemsSource = localeSelectData;

            string lastLocaleSelectedIndexSetting = _settings.Get(Constants.LastLocaleStorageKey);
            if (lastLocaleSelectedIndexSetting != null)
            {
                int lastLocaleSelectedIndex = int.Parse(lastLocaleSelectedIndexSetting);
                LocaleSelectList.SelectedIndex = lastLocaleSelectedIndex;
            }
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            SelectedVersion = (DefinitionSelectList.SelectedValue as DefinitionSelectData).Version;
            IsCanceled = false;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SelectedVersion = null;
            IsCanceled = true;
            Close();
        }

        public void SetDefinitionFromVersionDefinitions(VersionDefinitions[] versionDefinitions)
        {
            foreach (VersionDefinitions versionDefinition in versionDefinitions)
            {
                if (versionDefinition.buildRanges.Length > 0)
                {
                    foreach (BuildRange buildRange in versionDefinition.buildRanges)
                    {
                        definitionSelectData.Add(new DefinitionSelectData()
                        {
                            DisplayName = string.Format("{0} - {1}", buildRange.minBuild, buildRange.maxBuild),
                            Version = buildRange.maxBuild.ToString()
                        });
                    }
                }
                else
                {
                    foreach (Build build in versionDefinition.builds)
                    {
                        definitionSelectData.Add(new DefinitionSelectData()
                        {
                            DisplayName = build.ToString(),
                            Version = build.ToString()
                        });
                    }
                }
            }

            DefinitionSelectList.ItemsSource = definitionSelectData
                .OrderByDescending(e => e.Version)
                .Prepend(new DefinitionSelectData() { DisplayName = "Autoselect", Version = null })
                .ToList();
        }

        private void LocaleSelected(object sender, SelectionChangedEventArgs e)
        {
            LocaleSelectInfo localSelectData = (LocaleSelectList.SelectedItem as LocaleSelectInfo);

            if (localSelectData.Locale != SelectedLocale)
            {
                SelectedLocale = localSelectData.Locale;
                _settings.Store(Constants.LastLocaleStorageKey, LocaleSelectList.SelectedIndex.ToString());
            }
        }
    }

    class DefinitionSelectData
    {
        public string DisplayName { get; set; }
        public string Version { get; set; } = null;
    }

    class LocaleSelectInfo
    {
        public string DisplayName { get; set; }
        public Locale Locale { get; set; }
    }
}
