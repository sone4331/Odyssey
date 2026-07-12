using NUnit.Framework;
using Odyssey.Core.Abilities;
using Odyssey.Core.FSM;
using Odyssey.Core.Tags;
using Odyssey.Gameplay.AI;
using Odyssey.Gameplay.Combat;
using Odyssey.Unity.Save;
using Odyssey.Unity.UI;

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
    }
}
