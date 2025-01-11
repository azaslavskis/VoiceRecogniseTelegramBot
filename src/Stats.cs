

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace VoiceRecogniseBot
{
    public class StatsManager
    {
        private static SettingsPathClass settingsPath = new SettingsPathClass();
        private readonly string statsFilePath = settingsPath.GetStatsPath();
            
        private StatsData stats;

        public StatsManager()
        {
            LoadStats();
        }

        private void LoadStats()
        {
            if (File.Exists(statsFilePath))
            {
                string jsonData = File.ReadAllText(statsFilePath);
                stats = JsonConvert.DeserializeObject<StatsData>(jsonData) ?? new StatsData();
            }
            else
            {
                stats = new StatsData();
            }
        }

        public void IncrementMessageCount()
        {
            stats.TotalMessages++;
            stats.DailyMessages.Add(DateTime.UtcNow.Date);

            // Keep only messages from the past 7 days
            stats.DailyMessages.RemoveAll(date => (DateTime.UtcNow.Date - date).TotalDays > 7);

            SaveStats();
        }

        private void SaveStats()
        {
            string jsonData = JsonConvert.SerializeObject(stats, Formatting.Indented);
            File.WriteAllText(statsFilePath, jsonData);
        }

        public string GenerateJsonStats()
        {
            return JsonConvert.SerializeObject(new
            {
                stats.TotalMessages,
                MessagesPast7Days = stats.DailyMessages.Count
            }, (Formatting)Formatting.Indented);
        }
    }

    public class StatsData
    {
        public int TotalMessages { get; set; } = 0;
        public List<DateTime> DailyMessages { get; set; } = new List<DateTime>();
    }
}
