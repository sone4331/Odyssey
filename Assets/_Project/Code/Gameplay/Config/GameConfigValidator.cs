using System;
using System.Collections.Generic;

namespace Odyssey.Gameplay.Config
{
    /// <summary>
    /// 表示一次配置校验产生的不可变错误集合，供导表与构建门禁统一消费。
    /// </summary>
    public readonly struct ConfigValidationResult
    {
        public ConfigValidationResult(IReadOnlyList<string> errors)
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors == null || Errors.Count == 0;
    }

    /// <summary>
    /// 集中维护 Gameplay 配置的领域范围约束，不负责读取文件或创建 Unity 资产。
    /// 使用 Specification 风格的无状态验证，使 Editor 导入、命令行构建和测试共享同一规则来源。
    /// </summary>
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
                errors.Add("WalkSpeed 必须大于零。");
            }

            if (config.RunSpeed < config.WalkSpeed)
            {
                errors.Add("RunSpeed 必须大于或等于 WalkSpeed。");
            }

            if (config.Gravity >= 0f) errors.Add("Gravity 必须小于零。");
            if (config.DashForce <= 0f) errors.Add("DashForce 必须大于零。");
            if (config.DashDuration <= 0f) errors.Add("DashDuration 必须大于零。");
            if (config.DashCooldown < 0f) errors.Add("DashCooldown 不能小于零。");
            if (config.JumpHeight <= 0f) errors.Add("JumpHeight 必须大于零。");
            if (config.ChargeJumpHeight < config.JumpHeight) errors.Add("ChargeJumpHeight 不能小于 JumpHeight。");
            if (config.MinChargeTime < 0f) errors.Add("MinChargeTime 不能小于零。");
            if (config.AirJumpHeight <= 0f) errors.Add("AirJumpHeight 必须大于零。");
            if (config.WallSlideSpeed >= 0f) errors.Add("WallSlideSpeed 必须小于零。");
            if (config.WallJumpUpForce <= 0f) errors.Add("WallJumpUpForce 必须大于零。");
            if (config.WallJumpSideForce <= 0f) errors.Add("WallJumpSideForce 必须大于零。");
            if (config.AttackDamage <= 0) errors.Add("AttackDamage 必须大于零。");
            if (config.AttackRange <= 0f) errors.Add("AttackRange 必须大于零。");
            if (config.AttackCooldown < 0f) errors.Add("AttackCooldown 不能小于零。");
            if (config.MaxHealth <= 0) errors.Add("MaxHealth 必须大于零。");

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
                errors.Add("ChaseRange 必须大于零。");
            }

            if (config.AttackRange <= 0f || config.AttackRange > config.ChaseRange)
            {
                errors.Add("AttackRange 必须为正数且不能大于 ChaseRange。");
            }

            if (config.MaxHealth <= 0) errors.Add("MaxHealth 必须大于零。");
            if (config.AttackDamage <= 0) errors.Add("AttackDamage 必须大于零。");
            if (config.AttackCooldown < 0f) errors.Add("AttackCooldown 不能小于零。");

            return new ConfigValidationResult(errors);
        }

        private static void ValidateId(string id, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add("配置 ID 不能为空。");
            }
        }
    }
}
