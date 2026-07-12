using System;
using System.Collections.Generic;

namespace Odyssey.Gameplay.Config
{
    public readonly struct ConfigValidationResult
    {
        public ConfigValidationResult(IReadOnlyList<string> errors)
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors == null || Errors.Count == 0;
    }

    public static class GameConfigValidator
    {
        public static ConfigValidationResult Validate(PlayerConfigData config)
        {
            var errors = new List<string>();
            ValidateId(config?.Id, errors);
            if (config == null)
            {
                return new ConfigValidationResult(errors);
            }

            if (config.WalkSpeed <= 0f)
            {
                errors.Add("WalkSpeed must be greater than zero.");
            }

            if (config.RunSpeed < config.WalkSpeed)
            {
                errors.Add("RunSpeed must be greater than or equal to WalkSpeed.");
            }

            return new ConfigValidationResult(errors);
        }

        public static ConfigValidationResult Validate(EnemyConfigData config)
        {
            var errors = new List<string>();
            ValidateId(config?.Id, errors);
            if (config == null)
            {
                return new ConfigValidationResult(errors);
            }

            if (config.ChaseRange <= 0f)
            {
                errors.Add("ChaseRange must be greater than zero.");
            }

            if (config.AttackRange <= 0f || config.AttackRange > config.ChaseRange)
            {
                errors.Add("AttackRange must be positive and no greater than ChaseRange.");
            }

            return new ConfigValidationResult(errors);
        }

        private static void ValidateId(string id, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add("Config id cannot be empty.");
            }
        }
    }
}
