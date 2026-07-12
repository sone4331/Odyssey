using NUnit.Framework;
using Odyssey.Core.Abilities;
using Odyssey.Core.FSM;
using Odyssey.Core.Tags;
using Odyssey.Gameplay.AI;
using Odyssey.Gameplay.Combat;
using Odyssey.Unity.Save;

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

        [System.Serializable]
        private sealed class TestSaveData
        {
            public int Version;
            public int Health;
        }
    }
}
