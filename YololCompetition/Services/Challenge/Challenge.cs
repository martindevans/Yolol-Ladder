﻿using System;
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
        Approximate = 2,
    }

    public enum YololChip
    {
        Unknown = 0,

        Basic = 1,
        Advanced = 2,
        Professional = 3,
    }

    public class Challenge
    {
        private static readonly JsonSerializerSettings JsonConfig = new() {
            Converters = new JsonConverter[] {
                new YololValueConverter()
            },
            FloatFormatHandling = FloatFormatHandling.DefaultValue,
            FloatParseHandling = FloatParseHandling.Decimal
        };

        public ulong Id { get; }
        public string Name { get; }
        public ChallengeDifficulty Difficulty { get; set; }
        public string Description { get; }
        public ChallengeStatus Status { get; }

        public DateTime? EndTime { get; set; }

        public string CheckIndicator { get; }
        public IReadOnlyList<IReadOnlyDictionary<string, Value>> Inputs { get; }
        public IReadOnlyList<IReadOnlyDictionary<string, Value>> Outputs { get; }

        public bool ShuffleTests { get; }
        public ScoreMode ScoreMode { get; }
        public YololChip Chip { get; }

        public Yolol.Grammar.AST.Program Intermediate { get; }

        public Challenge(
            ulong id,
            string name,
            string checkIndicator,
            IReadOnlyList<IReadOnlyDictionary<string, Value>> inputs,
            IReadOnlyList<IReadOnlyDictionary<string, Value>> outputs,
            DateTime? endTime,
            ChallengeDifficulty difficulty,
            string description,
            bool shuffle,
            ScoreMode scoreMode,
            YololChip chip,
            Yolol.Grammar.AST.Program intermediate,
            ChallengeStatus status
        )
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
            Chip = chip;
            Intermediate = intermediate;
            Status = status;
        }

        public void Write(DbParameterCollection output)
        {
            output.Add(new SqliteParameter("@ID", DbType.UInt64) { Value = Id });
            output.Add(new SqliteParameter("@Name", DbType.String) { Value = Name });
            output.Add(new SqliteParameter("@CheckIndicator", DbType.String) { Value = CheckIndicator });
            output.Add(new SqliteParameter("@Difficulty", DbType.Int32) { Value = (int)Difficulty });
            output.Add(new SqliteParameter("@Description", DbType.String) { Value = Description });
            output.Add(new SqliteParameter("@Shuffle", DbType.UInt64) { Value = Convert.ToUInt64(ShuffleTests) });
            output.Add(new SqliteParameter("@ScoreMode", DbType.UInt64) { Value = (int)ScoreMode });
            output.Add(new SqliteParameter("@Chip", DbType.UInt64) { Value = (int)Chip });

            var i = JsonConvert.SerializeObject(Inputs, JsonConfig);
            output.Add(new SqliteParameter("@Inputs", DbType.String) { Value = i });

            var o = JsonConvert.SerializeObject(Outputs, JsonConfig);
            output.Add(new SqliteParameter("@Outputs", DbType.String) { Value = o });

            output.Add(new SqliteParameter("@EndUnixTime", DbType.UInt64) { Value = EndTime?.UnixTimestamp() });

            output.Add(new SqliteParameter("@IntermediateCode", DbType.String) { Value = Intermediate.ToString() });

            output.Add(new SqliteParameter("@Status", DbType.UInt64) { Value = (int)Status });
        }

        public static Challenge Read(DbDataReader reader)
        {
            var endUnixTimeObj = reader["EndUnixTime"];
            DateTime? end = null;
            if (endUnixTimeObj != DBNull.Value)
            {
                var endStr = endUnixTimeObj.ToString();
                if (endStr != null)
                    end = ulong.Parse(endStr).FromUnixTimestamp();
            }

            var code = reader["IntermediateCode"].ToString() ?? "";
            var parse = Yolol.Grammar.Parser.ParseProgram(code);
            if (!parse.IsOk)
                throw new InvalidOperationException($"Failed to parse program stored in DB:\n{parse.Err}");

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
                (ScoreMode)int.Parse(reader["ScoreMode"].ToString()!),
                (YololChip)int.Parse(reader["Chip"].ToString()!),
                parse.Ok,
                (ChallengeStatus)int.Parse(reader["Status"].ToString()!)
            );
        }
    }
}
