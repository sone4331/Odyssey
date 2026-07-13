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
        Spec.Run("Tick 返回后提交状态转移", TransitionIsCommittedAfterTickReturns);
        Spec.Run("自转移不会重新进入状态", SelfTransitionDoesNotReenterState);
    }

    private static void SelfTransitionDoesNotReenterState()
    {
        var idle = new RecordingState(StateId.Idle);
        var machine = new DeferredStateMachine<StateId>(new Dictionary<StateId, IState<StateId>>
        {
            [StateId.Idle] = idle
        });
        machine.Initialize(StateId.Idle);

        machine.Tick(0.016f);

        Spec.Equal(1, idle.EnterCount, "自转移重复进入状态");
        Spec.Equal(0, idle.ExitCount, "自转移错误退出状态");
    }

    private static void TransitionIsCommittedAfterTickReturns()
    {
        var idle = new RecordingState(StateId.Moving);
        var moving = new RecordingState();
        var machine = new DeferredStateMachine<StateId>(new Dictionary<StateId, IState<StateId>>
        {
            [StateId.Idle] = idle,
            [StateId.Moving] = moving
        });

        idle.DuringTick = () => Spec.Equal(StateId.Idle, machine.CurrentId, "Tick 期间当前状态被提前修改");

        machine.Initialize(StateId.Idle);
        machine.Tick(0.016f);

        Spec.Equal(StateId.Moving, machine.CurrentId, "Tick 后转移未被提交");
        Spec.Equal(1, idle.ExitCount, "旧状态未恰好退出一次");
        Spec.Equal(1, moving.EnterCount, "新状态未恰好进入一次");
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
