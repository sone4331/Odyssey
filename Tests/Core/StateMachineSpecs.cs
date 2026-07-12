using System;
using System.Collections.Generic;
using Odyssey.Core.FSM;

internal static class StateMachineSpecs
{
    private enum StateId
    {
        Idle,
        Moving
    }

    public static void Register()
    {
        Spec.Run("transition_is_committed_after_tick_returns", TransitionIsCommittedAfterTickReturns);
        Spec.Run("self_transition_does_not_reenter_state", SelfTransitionDoesNotReenterState);
    }

    private static void SelfTransitionDoesNotReenterState()
    {
        var idle = new RecordingState(StateId.Idle);
        var machine = new StateMachine<StateId>(new Dictionary<StateId, IState<StateId>>
        {
            [StateId.Idle] = idle
        });
        machine.Initialize(StateId.Idle);

        machine.Tick(0.016f);

        Spec.Equal(1, idle.EnterCount, "self transition re-entered state");
        Spec.Equal(0, idle.ExitCount, "self transition exited state");
    }

    private static void TransitionIsCommittedAfterTickReturns()
    {
        var idle = new RecordingState(StateId.Moving);
        var moving = new RecordingState();
        var machine = new StateMachine<StateId>(new Dictionary<StateId, IState<StateId>>
        {
            [StateId.Idle] = idle,
            [StateId.Moving] = moving
        });

        idle.DuringTick = () => Spec.Equal(StateId.Idle, machine.CurrentId, "current state changed during Tick");

        machine.Initialize(StateId.Idle);
        machine.Tick(0.016f);

        Spec.Equal(StateId.Moving, machine.CurrentId, "transition was not committed after Tick");
        Spec.Equal(1, idle.ExitCount, "old state did not exit exactly once");
        Spec.Equal(1, moving.EnterCount, "new state did not enter exactly once");
    }

    private sealed class RecordingState : IState<StateId>
    {
        private readonly StateId? _nextState;

        public RecordingState(StateId? nextState = null)
        {
            _nextState = nextState;
        }

        public int EnterCount { get; private set; }
        public int ExitCount { get; private set; }
        public Action DuringTick { get; set; }

        public void Enter() => EnterCount++;
        public void Exit() => ExitCount++;

        public StateTransition<StateId> Tick(float deltaTime)
        {
            DuringTick?.Invoke();
            return _nextState.HasValue
                ? StateTransition<StateId>.To(_nextState.Value)
                : StateTransition<StateId>.None;
        }
    }
}
