using NUnit.Framework;
using Odyssey.Core.Abilities;
using Odyssey.Core.FSM;
using Odyssey.Core.Tags;
using Odyssey.Gameplay.AI;
using Odyssey.Gameplay.Combat;
using Odyssey.Gameplay.Config;
using Odyssey.Unity.Save;
using Odyssey.Unity.UI;
using Odyssey.Unity.Config;
using Odyssey.Editor.Config;
using Odyssey.Inputs;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

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
        public void PlayerConfigBinder_AppliesSelectedConfigToRuntimeTarget()
        {
            var provider = new GameConfigDatabase(new PlayerConfigData("player", 7f, 11f));
            var target = new RecordingPlayerConfigTarget();

            PlayerConfigBinder.Bind(provider, "player", target);

            Assert.That(target.Config.WalkSpeed, Is.EqualTo(7f));
            Assert.That(target.Config.RunSpeed, Is.EqualTo(11f));
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
                maxHealth = 6
            };

            var data = entry.ToData();

            Assert.That(data.Gravity, Is.EqualTo(-18f));
            Assert.That(data.DashForce, Is.EqualTo(22f));
            Assert.That(data.AttackDamage, Is.EqualTo(2));
            Assert.That(data.MaxHealth, Is.EqualTo(6));
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

        private sealed class RecordingPlayerConfigTarget : IPlayerConfigTarget
        {
            public PlayerConfigData Config { get; private set; }

            public void Apply(PlayerConfigData config)
            {
                Config = config;
            }
        }
    }
}
