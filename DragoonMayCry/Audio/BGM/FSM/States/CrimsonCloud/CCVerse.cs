using DragoonMayCry.Audio.BGM;
using DragoonMayCry.Audio.BGM.FSM;
using DragoonMayCry.Audio.BGM.FSM.States;
using Lumina.Data.Parsing;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.TimeZoneInfo;

namespace DragoonMayCry.Audio.BGM.FSM.States.DevilTrigger
{
    internal class CCVerse : IFsmState
    {
        // loop between verse and riff
        enum CombatLoopState
        {
            Intro,
            CoreLoop,
            Exit
        }
        public BgmState ID { get { return BgmState.CombatLoop; } }

        private readonly Dictionary<BgmId, BgmTrackData> transitionTimePerId = new Dictionary<BgmId, BgmTrackData> {
            { BgmId.CombatEnter1, new BgmTrackData(0, 2500) },
            { BgmId.CombatEnter2, new BgmTrackData(0, 10500) },
            { BgmId.CombatEnter3, new BgmTrackData(0, 2600) },
            { BgmId.CombatVerse1, new BgmTrackData(0, 20650) },
            { BgmId.CombatVerse2, new BgmTrackData(0, 20650) },
            { BgmId.CombatVerse3, new BgmTrackData(0, 20600) },
            { BgmId.CombatVerse4, new BgmTrackData(0, 20600) },
            { BgmId.CombatCoreLoopTransition1, new BgmTrackData(0, 19355) },
            { BgmId.CombatCoreLoopTransition2, new BgmTrackData(1300, 21900) },
            { BgmId.CombatCoreLoopTransition3, new BgmTrackData(0, 10300) },
            { BgmId.CombatCoreLoopExit1, new BgmTrackData(1, 1000) },
            { BgmId.CombatCoreLoopExit2, new BgmTrackData(1, 2555) },
        };

        private readonly Dictionary<BgmId, string> bgmPaths = new Dictionary<BgmId, string> {
            { BgmId.CombatEnter1, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\004.ogg") },
            { BgmId.CombatEnter2, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\069.ogg") },
            { BgmId.CombatEnter3, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\036.ogg") },
            { BgmId.CombatVerse1, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\110.ogg") },
            { BgmId.CombatVerse2, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\056.ogg") },
            { BgmId.CombatVerse3, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\007.ogg") },
            { BgmId.CombatVerse4, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\074.ogg") },
            { BgmId.CombatCoreLoopTransition1, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\052.ogg") },
            { BgmId.CombatCoreLoopTransition2, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\060.ogg") },
            { BgmId.CombatCoreLoopTransition3, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\108.ogg") },
            { BgmId.CombatCoreLoopExit1, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\038.ogg") },
            { BgmId.CombatCoreLoopExit2, DynamicBgmService.GetPathToAudio("CrimsonCloud\\Verse\\045.ogg") },
        };

        private LinkedList<BgmId> combatLoop = new LinkedList<BgmId>();
        private readonly LinkedList<BgmId> combatIntro = new LinkedList<BgmId>();
        private LinkedListNode<BgmId>? currentTrack;
        private readonly AudioService audioService;
        private readonly Stopwatch currentTrackStopwatch;
        private CombatLoopState currentState;
        private int transitionTime = 0;
        private readonly Queue<ISampleProvider> samples;
        private readonly Random rand;
        public CCVerse(AudioService audioService)
        {
            rand = new Random();
            this.audioService = audioService;
            currentTrackStopwatch = new Stopwatch();
            samples = new Queue<ISampleProvider>();

            combatIntro.AddLast(BgmId.CombatEnter1);
            combatIntro.AddLast(BgmId.CombatEnter2);
            combatIntro.AddLast(BgmId.CombatEnter3);
        }

        public void Enter(bool fromVerse)
        {
            combatLoop = GenerateCombatLoop();
            currentTrackStopwatch.Restart();
            ISampleProvider? sample;
            if (fromVerse)
            {
                currentTrack = combatLoop.First!;
                sample = audioService.PlayBgm(currentTrack.Value, 1);
                currentState = CombatLoopState.CoreLoop;
            }
            else
            {
                currentTrack = combatIntro.First!;
                sample = audioService.PlayBgm(currentTrack.Value, 1);
                currentState = CombatLoopState.Intro;
            }
            if (sample != null)
            {
                samples.Enqueue(sample);
            }
            transitionTime = ComputeNextTransitionTiming();
            currentTrackStopwatch.Restart();
        }

        public void Update()
        {
            if (!currentTrackStopwatch.IsRunning)
            {
                return;
            }

            if (currentTrackStopwatch.IsRunning && currentTrackStopwatch.ElapsedMilliseconds >= transitionTime)
            {
                if (currentState != CombatLoopState.Exit)
                {
                    PlayNextPart();
                    currentTrackStopwatch.Restart();
                }
                else
                {
                    LeaveState();
                }

            }
        }

        private void PlayNextPart()
        {
            // transition to loop state if we reached the end of intro
            if (currentState == CombatLoopState.Intro)
            {
                if (currentTrack!.Next == null)
                {
                    currentTrack = combatLoop.First!;
                    currentState = CombatLoopState.CoreLoop;
                }
                else
                {
                    currentTrack = currentTrack.Next;
                }
            }
            else if (currentState == CombatLoopState.CoreLoop)
            {
                if (currentTrack!.Next != null)
                {
                    currentTrack = currentTrack.Next;
                }
                else
                {
                    currentTrack = combatLoop.First!;
                }
            }

            PlayBgmPart();
            transitionTime = ComputeNextTransitionTiming();
            currentTrackStopwatch.Restart();
        }

        private void PlayBgmPart()
        {
            if (samples.Count > 4)
            {
                samples.Dequeue();
            }

            var sample = audioService.PlayBgm(currentTrack!.Value, 1);
            if (sample != null)
            {
                samples.Enqueue(sample);
            }
        }

        private int ComputeNextTransitionTiming()
        {
            return transitionTimePerId[currentTrack!.Value].TransitionStart;
        }

        private void LeaveState()
        {
            
            while (samples.Count > 1)
            {
                audioService.RemoveBgmPart(samples.Dequeue());
            }
            if (samples.TryDequeue(out var sample))
            {
                if (sample is FadeInOutSampleProvider)
                {
                    ((FadeInOutSampleProvider)sample).BeginFadeOut(500);
                }
            }
            currentState = CombatLoopState.CoreLoop;
            currentTrackStopwatch.Reset();
        }

        public int Exit(ExitType exit)
        {
            var nextTransitionTime = transitionTimePerId[BgmId.CombatCoreLoopExit1].TransitionStart;
            if (currentState == CombatLoopState.Exit)
            {
                nextTransitionTime = (int)Math.Max(nextTransitionTime - currentTrackStopwatch.ElapsedMilliseconds, 0);
            }
            else
            {
                if (exit == ExitType.Promotion)
                {
                    audioService.PlayBgm(SelectRandom(BgmId.CombatCoreLoopExit1, BgmId.CombatCoreLoopExit2), 1);
                }
                else if (exit == ExitType.EndOfCombat)
                {
                    audioService.PlayBgm(BgmId.CombatEnd);
                    nextTransitionTime = 4500;
                }
                currentTrackStopwatch.Restart();
            }
            currentState = CombatLoopState.Exit;
            transitionTime = exit == ExitType.EndOfCombat ? 1 : transitionTimePerId[BgmId.CombatCoreLoopExit1].EffectiveStart;

            return nextTransitionTime;
        }

        public Dictionary<BgmId, string> GetBgmPaths()
        {
            return bgmPaths;
        }

        public void Reset()
        {
            LeaveState();
        }

        private BgmId SelectRandom(params BgmId[] bgmIds)
        {
            int index = rand.Next(0, bgmIds.Length);
            return bgmIds[index];
        }

        private Queue<BgmId> RandomizeQueue(params BgmId[] bgmIds)
        {
            var k = rand.Next(2);
            var queue = new Queue<BgmId>();
            if (k < 1)
            {
                for(int i = 0; i < bgmIds.Length; i++)
                {
                    queue.Enqueue(bgmIds[i]);
                }
            }
            else
            {
                for (int i = bgmIds.Length - 1; i >= 0; i--)
                {
                    queue.Enqueue(bgmIds[i]);
                }
            }
            return queue;
        }

        private LinkedList<BgmId> GenerateCombatLoop()
        {
            var verseQueue = RandomizeQueue(BgmId.CombatVerse1, BgmId.CombatVerse2);
            var verse2Queue = RandomizeQueue(BgmId.CombatVerse3, BgmId.CombatVerse4);
            var verse3Queue = RandomizeQueue(BgmId.CombatCoreLoopTransition1, BgmId.CombatCoreLoopTransition2);
            var loop = new LinkedList<BgmId>();
            loop.AddLast(verseQueue.Dequeue());
            loop.AddLast(verse2Queue.Dequeue());
            loop.AddLast(verse3Queue.Dequeue());
            loop.AddLast(BgmId.CombatCoreLoopTransition3);
            loop.AddLast(verseQueue.Dequeue());
            loop.AddLast(verse2Queue.Dequeue());
            loop.AddLast(verse3Queue.Dequeue());
            loop.AddLast(BgmId.CombatCoreLoopTransition3);
            return loop;
        }

        public bool CancelExit()
        {
            return false;
        }
    }
}
