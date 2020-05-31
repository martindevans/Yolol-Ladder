using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Yolol.Execution;
using YololCompetition.Extensions;
using YololCompetition.Serialization.Json;

namespace YololCompetition.Services.Challenge
{
    public enum ChallengeDifficulty
    {
        Unknown = 0,
        Easy = 1,
        Medium = 2,
        Hard = 3,
        Impossible = 4,
    }

    public enum ScoreMode
    {
        Unknown = 0,

        BasicScoring = 1,
    }

    public class Challenge
    {
        private static readonly JsonSerializerSettings JsonConfig = new JsonSerializerSettings {
            Converters = new JsonConverter[] {
                new YololValueConverter()
            },
            FloatFormatHandling = FloatFormatHandling.DefaultValue,
            FloatParseHandling = FloatParseHandling.Decimal
        };

        public ulong Id { get; }
        public string Name { get; }
        public ChallengeDifficulty Difficulty { get; }
        public string Description { get; }

        public DateTime? EndTime { get; }

        public string CheckIndicator { get; }
        public IReadOnlyList<IReadOnlyDictionary<string, Value>> Inputs { get; }
        public IReadOnlyList<IReadOnlyDictionary<string, Value>> Outputs { get; }

        public bool ShuffleTests { get; }
        public ScoreMode ScoreMode { get; }

        public Challenge(ulong id, string name, string checkIndicator, IReadOnlyList<IReadOnlyDictionary<string, Value>> inputs, IReadOnlyList<IReadOnlyDictionary<string, Value>> outputs, DateTime? endTime, ChallengeDifficulty difficulty, string description, bool shuffle, ScoreMode scoreMode)
        {
            Id = id;
            Name = name;
            CheckIndicator = checkIndicator;
            Inputs = inputs;
            Outputs = outputs;
            EndTime = endTime;
            Difficulty = difficulty;
            Description = description;
            ScoreMode = scoreMode;
            ShuffleTests = shuffle;
        }

        public void Write(DbParameterCollection output)
        {
            output.Add(new SqliteParameter("@Name", DbType.String) { Value = Name });
            output.Add(new SqliteParameter("@CheckIndicator", DbType.String) { Value = CheckIndicator });
            output.Add(new SqliteParameter("@Difficulty", DbType.Int32) { Value = (int)Difficulty });
            output.Add(new SqliteParameter("@Description", DbType.String) { Value = Description });
            output.Add(new SqliteParameter("@Shuffle", DbType.UInt64) { Value = Convert.ToUInt64(ShuffleTests) });
            output.Add(new SqliteParameter("@ScoreMode", DbType.UInt64) { Value = (int)ScoreMode });

            var i = JsonConvert.SerializeObject(Inputs, JsonConfig);
            output.Add(new SqliteParameter("@Inputs", DbType.String) { Value = i });

            var o = JsonConvert.SerializeObject(Outputs, JsonConfig);
            output.Add(new SqliteParameter("@Outputs", DbType.String) { Value = o });

            output.Add(new SqliteParameter("@EndUnixTime", DbType.UInt64) { Value = EndTime?.UnixTimestamp() });
        }

        public static Challenge Read(DbDataReader reader)
        {
            var endUnixTimeObj = reader["EndUnixTime"];
            DateTime? end = null;
            if (endUnixTimeObj != DBNull.Value)
            {
                var endStr = endUnixTimeObj?.ToString();
                if (endStr != null)
                    end = ulong.Parse(endStr).FromUnixTimestamp();
            }

            return new Challenge(
                ulong.Parse(reader["ID"].ToString()!),
                reader["Name"].ToString()!,
                reader["CheckIndicator"].ToString()!,
                JsonConvert.DeserializeObject<List<Dictionary<string, Value>>>(reader["Inputs"].ToString()!, JsonConfig)!,
                JsonConvert.DeserializeObject<List<Dictionary<string, Value>>>(reader["Outputs"].ToString()!, JsonConfig)!,
                end,
                (ChallengeDifficulty)int.Parse(reader["Difficulty"].ToString()!),
                reader["Description"].ToString()!,
                Convert.ToBoolean(ulong.Parse(reader["Shuffle"].ToString()!)),
                (ScoreMode)int.Parse(reader["ScoreMode"].ToString()!)
            );
        }
    }
}
