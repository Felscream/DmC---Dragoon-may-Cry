using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DragoonMayCry.Audio.BGM.FSM.States.DevilTrigger
{
    public class CCIntro : IFsmState
    {
        enum IntroState
        {
            OutOfCombat,
            CombatStart,
            EndCombat,
        }
        public BgmState ID { get { return BgmState.Intro; } }

        private readonly Dictionary<BgmId, BgmTrackData> transitionTimePerId = new()
        {
            { BgmId.Intro, new BgmTrackData(0, 85500) },
        };

        private readonly Dictionary<BgmId, string> bgmPaths = new()
        {
            { BgmId.Intro, DynamicBgmService.GetPathToAudio("CrimsonCloud\\intro.ogg") },
            { BgmId.CombatEnd, DynamicBgmService.GetPathToAudio("CrimsonCloud\\end.ogg") },
        };

        private readonly AudioService audioService;
        private readonly Stopwatch currentTrackStopwatch;
        private int transitionTime = 0;
        private readonly Queue<ISampleProvider> samples;
        private IntroState state = IntroState.OutOfCombat;
        private int nextStateTransitionTime = 0;

        public CCIntro(AudioService audioService)
        {
            currentTrackStopwatch = new Stopwatch();

            this.audioService = audioService;
            samples = new Queue<ISampleProvider>();
        }

        public void Enter(bool fromVerse)
        {
            state = IntroState.OutOfCombat;
            var sample = audioService.PlayBgm(BgmId.Intro, 4500);
            if (sample != null)
            {
                samples.Enqueue(sample);
            }

            transitionTime = transitionTimePerId[BgmId.Intro].TransitionStart;
            currentTrackStopwatch.Restart();
        }

        public void Update()
        {
            if (!currentTrackStopwatch.IsRunning)
            {
                return;
            }

            if (currentTrackStopwatch.Elapsed.TotalMilliseconds > transitionTime)
            {

                if (state != IntroState.OutOfCombat)
                {
                    TransitionToNextState();
                }
                else
                {
                    var sample = audioService.PlayBgm(BgmId.Intro);
                    if (sample != null)
                    {
                        samples.Enqueue(sample);
                    }
                    currentTrackStopwatch.Restart();
                }
            }
        }

        public void Reset()
        {
            while (samples.Count > 1)
            {
                audioService.RemoveBgmPart(samples.Dequeue());
            }
            if (samples.TryDequeue(out var sample))
            {
                if (sample is FadeInOutSampleProvider)
                {
                    ((FadeInOutSampleProvider)sample).BeginFadeOut(3000);
                }
            }
            else if (sample != null)
            {
                audioService.RemoveBgmPart(sample);
            }
            currentTrackStopwatch.Reset();
        }

        public Dictionary<BgmId, string> GetBgmPaths()
        {
            return bgmPaths;
        }

        public int Exit(ExitType exit)
        {
            if (!currentTrackStopwatch.IsRunning)
            {
                return 0;
            }
            if (exit == ExitType.ImmediateExit)
            {
                transitionTime = 0;
                TransitionToNextState();
                return 0;
            }

            // we are already leaving this state, player transitioned rapidly between multiple ranks
            if (state != IntroState.OutOfCombat)
            {
                nextStateTransitionTime = (int)Math.Max(nextStateTransitionTime - currentTrackStopwatch.Elapsed.TotalMilliseconds, 0);
            }
            else if (exit == ExitType.Promotion)
            {
                state = IntroState.CombatStart;
                nextStateTransitionTime = 0;
                transitionTime = 1600;
                currentTrackStopwatch.Restart();
            }

            if (exit == ExitType.EndOfCombat && state != IntroState.EndCombat)
            {
                state = IntroState.EndCombat;
                transitionTime = 100;
                nextStateTransitionTime = 4500;
                currentTrackStopwatch.Restart();
                audioService.PlayBgm(BgmId.CombatEnd);
            }
            return nextStateTransitionTime;
        }

        private void TransitionToNextState()
        {
            if (samples.TryDequeue(out var sample))
            {
                if (sample is FadeInOutSampleProvider)
                {
                    ((FadeInOutSampleProvider)sample).BeginFadeOut(1500);
                }
                else
                {
                    audioService.RemoveBgmPart(sample);
                }
            }

            currentTrackStopwatch.Reset();
        }

        public bool CancelExit()
        {
            return false;
        }
    }
}
