using Dalamud.Plugin.Services;
using DragoonMayCry.State;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using DragoonMayCry.Data;
using DragoonMayCry.Score.Action;
using DragoonMayCry.Util;
using DragoonMayCry.Score.Style;
using FFXIVClientStructs.FFXIV.Client.Game;
using static DragoonMayCry.Score.Style.StyleRankHandler;

namespace DragoonMayCry.Score
{
    public unsafe class ScoreManager : IDisposable

    {
        public class ScoreRank
        {
            public float Score { get; set; }
            public StyleType Rank { get; set; }
            public StyleScoring StyleScoring { get; set; }

            public ScoreRank(float score, StyleType styleRank, StyleScoring styleScoring)
            {
                Score = score;
                Rank = styleRank;
                StyleScoring = styleScoring;
            }
        }

        public struct StyleScoring
        {
            public float Threshold;
            public int ReductionPerSecond;
            public float DemotionThreshold;
            public float PointCoefficient;

            public StyleScoring(int threshold, int reductionPerSecond, int demotionThreshold, float pointCoefficient)
            {
                Threshold = threshold;
                ReductionPerSecond = reductionPerSecond;
                DemotionThreshold = demotionThreshold;
                PointCoefficient = pointCoefficient;
            }
        }

        public EventHandler<double>? OnScoring;
        public ScoreRank CurrentScoreRank { get; private set; }
        private readonly PlayerState playerState;
        private readonly StyleRankHandler rankHandler;
        private readonly ItemLevelCalculator itemLevelCalculator;

        private const int PointsReductionDuration = 7300; //milliseconds
        private bool isCastingLb;
        private Dictionary<StyleType, StyleScoring> jobScoringTable;
        private readonly Stopwatch pointsReductionStopwatch;

        public ScoreManager(StyleRankHandler styleRankHandler, PlayerActionTracker playerActionTracker)
        {
            pointsReductionStopwatch = new Stopwatch();

            playerState = PlayerState.GetInstance();
            playerState.RegisterJobChangeHandler(((sender, ids) => ResetScore()));
            playerState.RegisterInstanceChangeHandler(OnInstanceChange!);
            playerState.RegisterCombatStateChangeHandler(OnCombatChange!);
            playerState.RegisterJobChangeHandler(OnJobChange);
            playerState.RegisterDeathStateChangeHandler(OnDeath);

            itemLevelCalculator = new ItemLevelCalculator();

            this.rankHandler = styleRankHandler;
            this.rankHandler.StyleRankChange += OnRankChange!;

            playerActionTracker.OnFlyTextCreation += AddScore;
            playerActionTracker.OnGcdClip += OnGcdClip;
            playerActionTracker.OnLimitBreak += OnLimitBreakCast;
            playerActionTracker.OnLimitBreakCanceled += OnLimitBreakCanceled;

            Service.Framework.Update += UpdateScore;
            Service.ClientState.Logout += ResetScore;

            jobScoringTable = ScoringTable.DefaultScoringTable;
            var styleRank = styleRankHandler.CurrentStyle.Value;
            CurrentScoreRank = new(0, styleRank, jobScoringTable[styleRank]);

            ResetScore();
        }

        public void Dispose()
        {
            Service.Framework.Update -= UpdateScore;
            Service.ClientState.Logout -= ResetScore;
        }

        public void UpdateScore(IFramework framework)
        {
            if (!Plugin.CanRunDmc())
            {
                return;
            }

            if (CanDisableGcdClippingRestrictions())
            {
                DisablePointsGainedReduction();
            }

            if (isCastingLb)
            {
                CurrentScoreRank.Score +=
                    (float)(framework.UpdateDelta.TotalSeconds * CurrentScoreRank.StyleScoring.ReductionPerSecond * 100);
                
            }
            else
            {
                var scoreReduction =
                    (float)(framework.UpdateDelta.TotalSeconds *
                            CurrentScoreRank.StyleScoring.ReductionPerSecond);
                if (AreGcdClippingRestrictionsActive())
                {
                    scoreReduction *= 1.5f;
                }
                CurrentScoreRank.Score -= scoreReduction;
                
            }
            CurrentScoreRank.Score = Math.Clamp(
                CurrentScoreRank.Score, 0, CurrentScoreRank.StyleScoring.Threshold * 1.2f);
        }
        private void AddScore(object? sender, float val)
        {
            var points = val * CurrentScoreRank.StyleScoring.PointCoefficient;
            if (AreGcdClippingRestrictionsActive())
            {
                points *= 0.75f;
            }

            CurrentScoreRank.Score += points;
            if (CurrentScoreRank.Rank == StyleType.SSS)
            {
                CurrentScoreRank.Score = Math.Min(
                    CurrentScoreRank.Score, CurrentScoreRank.StyleScoring.Threshold);
            }
            OnScoring?.Invoke(this, points);
        }

        private bool CanDisableGcdClippingRestrictions() => pointsReductionStopwatch.IsRunning 
            && pointsReductionStopwatch.ElapsedMilliseconds > PointsReductionDuration;

        private void OnInstanceChange(object send, bool value)
        {
            ResetScore();
            Service.Log.Debug($"{itemLevelCalculator.CalculateCurrentItemLevel()}");
        }

        private void OnCombatChange(object send, bool enteringCombat)
        {
            if(enteringCombat)
            {
                //jobScoringTable = GetJobScoringTable();
                jobScoringTable = ScoringTable.DefaultScoringTable;
                ResetScore();
            }

            DisablePointsGainedReduction();
            isCastingLb = false;
        }

        private void OnLimitBreakCast(object? sender, PlayerActionTracker.LimitBreakEvent e)
        {
            isCastingLb = e.IsCasting;
            if (!isCastingLb)
            {
                CurrentScoreRank.Score = CurrentScoreRank.StyleScoring.Threshold;
            }
        }

        private void OnLimitBreakCanceled(object? sender, EventArgs e)
        {
            isCastingLb = false;
            ResetScore();
        }

        private void OnRankChange(object sender, RankChangeData data)
        {
            if (!jobScoringTable.ContainsKey(data.NewRank))
            {
                return;
            }

            var nextStyleScoring = jobScoringTable[data.NewRank];
            if ((int)CurrentScoreRank.Rank < (int)data.NewRank)
            {
                CurrentScoreRank.Score = (float)Math.Clamp(CurrentScoreRank.Score %
                                                           nextStyleScoring.Threshold, 0, nextStyleScoring.Threshold * 0.5); ;
            }
            else if (data.IsBlunder)
            {
                CurrentScoreRank.Score = 0f;
            }

            CurrentScoreRank.Rank = data.NewRank;
            CurrentScoreRank.StyleScoring = nextStyleScoring;
        }

        private void OnGcdClip(object? send, float clippingTime)
        {
            var newScore = CurrentScoreRank.Score - CurrentScoreRank.StyleScoring.Threshold * 0.3f;
            CurrentScoreRank.Score = Math.Max(newScore, 0);
            pointsReductionStopwatch.Restart();
        }

        private void DisablePointsGainedReduction()
        {
            pointsReductionStopwatch.Reset();
        }

        private void ResetScore()
        {
            CurrentScoreRank.Score = 0;
        }

        private bool AreGcdClippingRestrictionsActive()
        {
            return pointsReductionStopwatch.IsRunning;
        }

        private void OnJobChange(object? sender, JobIds jobId)
        {
            ResetScore();
            DisablePointsGainedReduction();
        }

        private Dictionary<StyleType, StyleScoring> GetJobScoringTable()
        {
            var job = playerState.GetCurrentJob();
            var ilvl = itemLevelCalculator.CalculateCurrentItemLevel();
            if (JobHelper.IsTank(job))
            {
                Service.Log.Debug($"Setting tank scoring table");
                return ScoringTable.GenerateTankScoring(ilvl);
            }

            if (JobHelper.IsHealer(job))
            {
                Service.Log.Debug($"Setting healer scoring table");
                return ScoringTable.GenerateHealerScoring(ilvl);
            }

            //BLM and PIC damage output comparable to melee
            //MCH raw damage output comparable to casters
            if ((JobHelper.IsCaster(job) || job == JobIds.MCH) && job != JobIds.BLM && job != JobIds.PCT) 
            {
                Service.Log.Debug($"Setting Caster scoring table");
                return ScoringTable.GenerateCasterScoring(ilvl);
            }

            if (JobHelper.IsPhysRange(job))
            {
                Service.Log.Debug($"Setting Phys Range scoring table");
                return ScoringTable.GeneratePhysRangeScoring(ilvl);
            }

            Service.Log.Debug($"Setting Melee scoring table");
            return ScoringTable.GenerateMeleeScoring(ilvl);
        }

        private void OnDeath(object? sender, bool isDead)
        {
            if (isDead)
            {
                DisablePointsGainedReduction();
                ResetScore();
            }
        }
    }
}
