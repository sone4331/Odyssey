using NUnit.Framework;
using Odyssey.Core.Abilities;
using Odyssey.Core.FSM;
using Odyssey.Core.Tags;
using Odyssey.Bootstrap;
using Odyssey.Gameplay.AI;
using Odyssey.Gameplay.Combat;
using Odyssey.Gameplay.Config;
using Odyssey.Gameplay.Save;
using Odyssey.Unity.Save;
using Odyssey.Unity.UI;
using Odyssey.Unity.Config;
using Odyssey.Editor.Config;
using Odyssey.Inputs;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.Animations.Rigging;

namespace Odyssey.Tests
{
    public sealed class UnityAdapterTests
    {
        [Test]
        public void JsonSaveCodec_RoundTripsSerializableData()
        {
            var codec = new JsonSaveCodec<TestSaveData>();
            var expected = new TestSaveData { Version = 2, Health = 4 };

            var actual = codec.Decode(codec.Encode(expected));

            Assert.That(actual.Version, Is.EqualTo(2));
            Assert.That(actual.Health, Is.EqualTo(4));
        }

        [Test]
        public void PauseRuntime_UpdatesPanelAndRestoresGlobalTimeScale()
        {
            var panel = new GameObject("测试暂停面板");
            var previousTimeScale = Time.timeScale;
            var runtime = new PauseRuntime(panel);
            try
            {
                runtime.SetPaused(true);

                Assert.That(runtime.IsPaused, Is.True);
                Assert.That(panel.activeSelf, Is.True);
                Assert.That(Time.timeScale, Is.Zero);

                runtime.SetPaused(false);

                Assert.That(runtime.IsPaused, Is.False);
                Assert.That(panel.activeSelf, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                runtime.Dispose();
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(panel);
            }
        }

        [Test]
        public void Health_ClampsLethalDamage()
        {
            var health = new Health(3);

            var result = health.Apply(new DamageRequest(9, "test"));

            Assert.That(health.Current, Is.Zero);
            Assert.That(result.AppliedAmount, Is.EqualTo(3));
        }

        [Test]
        public void GameplayTagSet_MatchesParents()
        {
            var tags = new GameplayTagSet();
            tags.Add(GameplayTag.Parse("State.Combat.Attacking"));

            Assert.That(tags.Has(GameplayTag.Parse("State.Combat")), Is.True);
        }

        [Test]
        public void AbilitySystem_RejectsBlockedAbility()
        {
            var blocked = GameplayTag.Parse("State.Stunned");
            var system = new AbilitySystem(new[]
            {
                new AbilityDefinition("attack", blockedTags: new[] { blocked })
            });
            system.AddTag(blocked);

            var result = system.TryActivate("attack", 0f);

            Assert.That(result.Failure, Is.EqualTo(AbilityActivationFailure.BlockedByTag));
        }

        [Test]
        public void UtilitySelector_PicksHighestScore()
        {
            var selector = new UtilityGoalSelector<string, float>(
                new UtilityGoal<string, float>("patrol", _ => true, _ => 0.1f),
                new UtilityGoal<string, float>("chase", _ => true, _ => 0.9f));

            Assert.That(selector.Select(0f).Goal, Is.EqualTo("chase"));
        }

        [Test]
        public void HealthDisplayPresenter_RefreshesFromEventsAndFlashesOnlyOnDamage()
        {
            var view = new RecordingHealthDisplay();
            var presenter = new HealthDisplayPresenter(view, 5);

            presenter.Initialize(5);
            presenter.Handle(new HealthChanged(5, 3, "enemy"));
            presenter.Handle(new HealthChanged(3, 4, "heal"));

            Assert.That(view.Current, Is.EqualTo(4));
            Assert.That(view.Maximum, Is.EqualTo(5));
            Assert.That(view.RefreshCount, Is.EqualTo(3));
            Assert.That(view.DamageFlashCount, Is.EqualTo(1));
        }

        [Test]
        public void HealthDisplayPresenter_ReconfiguresMaximumHealth()
        {
            var view = new RecordingHealthDisplay();
            var presenter = new HealthDisplayPresenter(view, 5);

            presenter.Reconfigure(6, 7);

            Assert.That(view.Current, Is.EqualTo(6));
            Assert.That(view.Maximum, Is.EqualTo(7));
        }

        [Test]
        public void GameConfigBinder_AppliesSelectedConfigToTypedTarget()
        {
            var provider = new GameConfigDatabase(new PlayerConfigData("player", 7f, 11f));
            var target = new RecordingPlayerConfigTarget();

            GameConfigBinder.Bind(provider, "player", target);

            Assert.That(target.Config.WalkSpeed, Is.EqualTo(7f));
            Assert.That(target.Config.RunSpeed, Is.EqualTo(11f));
        }

        [Test]
        public void Enemy_UsesSharedHealthAndTypedConfiguration()
        {
            var root = new GameObject("测试敌人");
            var enemy = root.AddComponent<Odyssey.Characters.Enemies.Enemy>();
            try
            {
                var config = new EnemyConfigData(
                    "chomper", 10f, 2f,
                    maxHealth: 4,
                    attackDamage: 2,
                    attackCooldown: 1.5f);

                GameConfigBinder.Bind(new GameConfigDatabase(config), "chomper", enemy);
                enemy.TakeDamage(1);

                Assert.That(enemy.CurrentHealth, Is.EqualTo(3));
                Assert.That(enemy.AttackDamage, Is.EqualTo(2));
                Assert.That(enemy.AttackCooldown, Is.EqualTo(1.5f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayerConfigEntry_ConvertsCompleteCombatConfiguration()
        {
            var entry = new PlayerConfigEntry
            {
                id = "player",
                walkSpeed = 6f,
                runSpeed = 10f,
                gravity = -18f,
                dashForce = 22f,
                attackDamage = 2,
                maxHealth = 6,
                groundAcceleration = 20f,
                groundDeceleration = 25f,
                minTurnSpeed = 400f,
                maxTurnSpeed = 1200f,
                attackAdvanceSpeed = 1.5f
            };

            var data = entry.ToData();

            Assert.That(data.Gravity, Is.EqualTo(-18f));
            Assert.That(data.DashForce, Is.EqualTo(22f));
            Assert.That(data.AttackDamage, Is.EqualTo(2));
            Assert.That(data.MaxHealth, Is.EqualTo(6));
            Assert.That(data.GroundAcceleration, Is.EqualTo(20f));
            Assert.That(data.MaxTurnSpeed, Is.EqualTo(1200f));
            Assert.That(data.AttackAdvanceSpeed, Is.EqualTo(1.5f));
        }

        [Test]
        public void PlayerAnimator_ContainsOnlyCodeDrivenStatesAndSpeedParameter()
        {
            const string path = "Assets/_Project/Content/Animations/Player/PlayerAnimator.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            Assert.That(controller, Is.Not.Null, "未找到玩家 Animator Controller");
            Assert.That(controller.parameters.Length, Is.EqualTo(2),
                "代码驱动动画不应继续保留旧 Trigger、Bool 或连击参数");
            Assert.That(System.Array.Exists(controller.parameters, parameter =>
                parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float), Is.True);
            Assert.That(System.Array.Exists(controller.parameters, parameter =>
                parameter.name == "VerticalSpeed" && parameter.type == AnimatorControllerParameterType.Float), Is.True);

            var stateMachine = controller.layers[0].stateMachine;
            Assert.That(stateMachine.anyStateTransitions, Is.Empty,
                "代码直接 CrossFade 后不应保留 Any State 条件过渡");
            var stateNames = new System.Collections.Generic.HashSet<string>();
            foreach (var child in stateMachine.states)
            {
                stateNames.Add(child.state.name);
            }

            foreach (var required in new[]
                     {
                         "Locomotion", "Airborne", "Landing", "Dash",
                         "EllenCombo_1", "EllenCombo_2", "EllenCombo_3", "EllenCombo_4",
                         "EllenHitFront", "EllenDeath"
                     })
            {
                Assert.That(stateNames.Contains(required), Is.True, $"Animator 缺少状态：{required}");
            }

            foreach (var child in stateMachine.states)
            {
                Assert.That(child.state.transitions, Is.Empty,
                    $"状态 {child.state.name} 仍保留与代码驱动重复的旧过渡");
            }

            var locomotion = System.Array.Find(stateMachine.states, child => child.state.name == "Locomotion").state;
            var locomotionTree = locomotion.motion as BlendTree;
            Assert.That(locomotionTree, Is.Not.Null, "移动状态未使用 BlendTree");
            Assert.That(locomotionTree.children.Length, Is.EqualTo(3), "移动混合应包含待机、步行和跑步");
            Assert.That(locomotionTree.children[0].threshold, Is.EqualTo(0f));
            Assert.That(locomotionTree.children[1].threshold, Is.EqualTo(0.5f));
            Assert.That(locomotionTree.children[2].threshold, Is.EqualTo(1f));

            var airborne = System.Array.Find(stateMachine.states, child => child.state.name == "Airborne").state;
            Assert.That((airborne.motion as BlendTree)?.children.Length, Is.EqualTo(6),
                "空中混合应覆盖起跳、上升、顶点和下落六段姿势");
        }

        [Test]
        public void PlayerMovementMath_ProjectsDirectionOntoSlopeWithoutChangingMagnitude()
        {
            var normal = Quaternion.Euler(30f, 0f, 0f) * Vector3.up;

            var projected = Odyssey.Characters.Player.PlayerMovementMath.ProjectDirectionOnGround(
                Vector3.forward,
                normal);

            Assert.That(Vector3.Dot(projected, normal), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(projected.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void EllenPrefab_UsesCalibratedControllerAndCompleteFootRig()
        {
            const string path = "Assets/_Project/Content/Prefabs/Characters/Ellen.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, "未找到 Ellen Prefab");

            var animator = prefab.GetComponent<Animator>();
            var controller = prefab.GetComponent<CharacterController>();
            var rigBuilder = prefab.GetComponent<RigBuilder>();
            var placement = prefab.GetComponent<Odyssey.Characters.Player.PlayerFootPlacementController>();

            Assert.That(animator.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal));
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(controller.height, Is.EqualTo(1.8f).Within(0.001f));
            Assert.That(controller.center.y, Is.EqualTo(0.901f).Within(0.001f));
            Assert.That(controller.radius, Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(controller.skinWidth, Is.EqualTo(0.035f).Within(0.001f));
            Assert.That(rigBuilder, Is.Not.Null, "Prefab 缺少 RigBuilder");
            Assert.That(rigBuilder.layers.Count, Is.EqualTo(1), "脚部 RigLayer 数量错误");
            Assert.That(placement, Is.Not.Null, "Prefab 缺少脚部贴地控制器");
            var footConstraints = prefab.GetComponentsInChildren<TwoBoneIKConstraint>(true);
            Assert.That(footConstraints.Length, Is.EqualTo(2));
            foreach (var constraint in footConstraints)
            {
                Assert.That(constraint.data.targetPositionWeight, Is.EqualTo(1f),
                    $"{constraint.name} 未启用脚掌位置修正");
                Assert.That(constraint.data.targetRotationWeight, Is.EqualTo(0f),
                    $"{constraint.name} 不应覆盖 Generic 脚骨旋转");
            }

            Assert.That(prefab.GetComponentInChildren<OverrideTransform>(true), Is.Not.Null,
                "Prefab 缺少骨盆补偿约束");
        }

        [Test]
        public void GameConfigImportTrigger_OnlySchedulesSourceCsvOnceUntilExecuted()
        {
            var trigger = new GameConfigImportTrigger(
                "Assets/_Project/Data/Design/Player.csv",
                "Assets/_Project/Data/Design/Enemy.csv");
            System.Action queued = null;
            var importCount = 0;

            var ignored = trigger.TrySchedule(
                new[] { "Assets/_Project/Data/Runtime/Resources/Config/GameConfigDatabase.asset" },
                action => queued = action,
                () => importCount++);
            var first = trigger.TrySchedule(
                new[] { "Assets/_Project/Data/Design/Player.csv" },
                action => queued = action,
                () => importCount++);
            var duplicate = trigger.TrySchedule(
                new[] { "Assets/_Project/Data/Design/Enemy.csv" },
                action => queued = action,
                () => importCount++);

            Assert.That(ignored, Is.False);
            Assert.That(first, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(importCount, Is.Zero);

            queued.Invoke();

            Assert.That(importCount, Is.EqualTo(1));
            Assert.That(trigger.TrySchedule(
                new[] { "Assets/_Project/Data/Design/Enemy.csv" },
                action => queued = action,
                () => importCount++), Is.True);
        }

        [Test]
        public void ApplicationContext_ExposesExplicitApplicationServices()
        {
            var configs = new GameConfigDatabase();
            var saves = new RecordingPlayerSaveService();

            var context = new ApplicationContext(configs, saves);

            Assert.That(context.Configs, Is.SameAs(configs));
            Assert.That(context.SaveService, Is.SameAs(saves));
        }

        [Test]
        public void GameplaySceneInstaller_ReinstallingSameContextIsIdempotent()
        {
            var root = new GameObject("测试场景安装器");
            var configs = new GameConfigDatabase();
            var context = new ApplicationContext(configs, new RecordingPlayerSaveService());
            var installer = root.AddComponent<GameplaySceneInstaller>();
            try
            {
                installer.Install(context);
                Assert.DoesNotThrow(() => installer.Install(context));
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void HealthIconPool_GrowsHidesAndReusesIcons()
        {
            var container = new GameObject("生命图标容器", typeof(RectTransform));
            var seeds = new Image[5];
            var texture = new Texture2D(1, 1);
            var full = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            try
            {
                for (var index = 0; index < seeds.Length; index++)
                {
                    var icon = new GameObject($"生命图标 {index + 1}", typeof(RectTransform), typeof(Image));
                    icon.transform.SetParent(container.transform, false);
                    seeds[index] = icon.GetComponent<Image>();
                }

                var pool = new HealthIconPool(seeds);

                Assert.That(pool.SetHealth(6, 6, full, null), Is.True);
                Assert.That(pool.Count, Is.EqualTo(6));
                Assert.That(pool.ActiveCount, Is.EqualTo(6));

                pool.SetHealth(6, 6, full, null);
                Assert.That(pool.Count, Is.EqualTo(6));

                pool.SetHealth(3, 4, full, null);
                Assert.That(pool.Count, Is.EqualTo(6));
                Assert.That(pool.ActiveCount, Is.EqualTo(4));
                Assert.That(pool[0].sprite, Is.SameAs(full));
                Assert.That(pool[2].sprite, Is.SameAs(full));
                Assert.That(pool[3].sprite, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(full);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(container);
            }
        }

        [Test]
        public void ProjectStructure_UsesMigratedSceneAndPreservesCriticalGuids()
        {
            const string scenePath = "Assets/_Project/Content/Scenes/Level_01.unity";
            const string healthUiPath = "Assets/_Project/Code/Unity/UI/PlayerHealthUI.cs";
            const string playerPath = "Assets/_Project/Code/Unity/Characters/Player/PlayerController.cs";
            const string inputReaderPath = "Assets/_Project/Data/UnityAssets/Input/PlayerInputReader.asset";

            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Length, Is.EqualTo(1));
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(scenePath));
            Assert.That(AssetDatabase.AssetPathToGUID(healthUiPath), Is.EqualTo("752cb09d697c4375af4ea10b03fe7ca5"));
            Assert.That(AssetDatabase.AssetPathToGUID(playerPath), Is.EqualTo("cd9defc8c3d24d3d971a34755db37fe1"));
            Assert.That(AssetDatabase.LoadAssetAtPath<InputReader>(inputReaderPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Object>("Assets/_Project/Scenes/Level_01.unity"), Is.Null);
            Assert.That(AssetDatabase.IsValidFolder("Assets/_Project/Code"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder("Assets/_Project/Content"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder("Assets/_Project/Data"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder("Assets/_Project/Scripts"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/_Project/MyScripts"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/_Project/Generated"), Is.False);
        }

        [System.Serializable]
        private sealed class TestSaveData
        {
            public int Version;
            public int Health;
        }

        private sealed class RecordingHealthDisplay : IHealthDisplayView
        {
            public int Current { get; private set; }
            public int Maximum { get; private set; }
            public int RefreshCount { get; private set; }
            public int DamageFlashCount { get; private set; }

            public void SetHealth(int current, int maximum)
            {
                Current = current;
                Maximum = maximum;
                RefreshCount++;
            }

            public void ShowDamageFlash()
            {
                DamageFlashCount++;
            }
        }

        private sealed class RecordingPlayerConfigTarget : IConfigTarget<PlayerConfigData>
        {
            public PlayerConfigData Config { get; private set; }

            public void Apply(PlayerConfigData config)
            {
                Config = config;
            }
        }

        private sealed class RecordingPlayerSaveService : ISaveService<PlayerSaveData>
        {
            public void Save(PlayerSaveData data)
            {
            }

            public bool TryLoad(out PlayerSaveData data)
            {
                data = null;
                return false;
            }
        }
    }
}
