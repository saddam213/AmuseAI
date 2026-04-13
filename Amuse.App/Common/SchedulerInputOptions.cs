using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TensorStack.Python.Scheduler;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public record SchedulerInputOptions : BaseRecord
    {
        private SchedulerType _scheduler;
        private int _numTrainTimesteps = 0;
        private int _originalInferenceSteps;
        private int _baseImageSeqLen;
        private int _maxImageSeqLen;
        private BetaScheduleType _betaSchedule;
        private float _betaStart = 0f;
        private float _betaEnd = 0f;
        private PredictionType _predictionType;
        private TimestepSpacingType _timestepSpacing;
        private int _stepsOffset = 0;
        private bool _clipSample = false;
        private float _clipSampleRange = 0f;
        private float _sampleMaxValue = 0f;
        private bool _thresholding = false;
        private float _dynamicThresholdingRatio = 0f;
        private VarianceType? _varianceType;
        private bool _useKarrasSigmas;
        private bool _useBetaSigmas;
        private bool _useExponentialSigmas;
        private bool _useFlowSigmas;
        private float _sigmaMin;
        private float _sigmaMax;
        private FinalSigmasType _finalSigmasType;
        private InterpolationType _interpolationType;
        private TimestepType _timestepType;
        private bool _rescaleBetasZeroSNR;
        private bool _setAlphaToOne;
        private float _timestepScaling;
        private float _shift = 0f;
        private float _baseShift = 0f;
        private float _maxShift = 0f;
        private float? _shiftTerminal;
        private bool _useDynamicShifting;
        private float _flowShift = 0;
        private float _sNRShiftScale;
        private TimeShiftType _timeShiftType;
        private float _rho = 0f;
        private int _solverOrder = 0;
        private SolverType _solverType;
        private AlgorithmType _algorithmType;
        private bool _lowerOrderFinal;
        private bool _stochasticSampling;
        private float _eta = 0.0f;
        private float _sNoise = 0f;
        private bool _invertSigmas;
        private bool _skipPrkSteps;
        private bool _predictX0;
        private bool _eulerAtFinal;
        private bool _useLuLambdas;
        private int? _noiseSamplerSeed;
        private float _sigmaData;
        private SigmaScheduleType _sigmaScheduleType;
        private UpscaleModeType _upscaleMode;
        private int _stages;
        private float _gamma;
        private int _predictorOrder;
        private int _correctorOrder;
        private List<float> _scaleFactors;
        private List<float> _stageRange;
        private List<int> _disableCorrector;
        private float _sValue;
        private float _scaler;

        public SchedulerType Scheduler
        {
            get { return _scheduler; }
            set { SetProperty(ref _scheduler, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int NumTrainTimesteps
        {
            get { return _numTrainTimesteps; }
            set { SetProperty(ref _numTrainTimesteps, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int OriginalInferenceSteps
        {
            get { return _originalInferenceSteps; }
            set { SetProperty(ref _originalInferenceSteps, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int BaseImageSeqLen
        {
            get { return _baseImageSeqLen; }
            set { SetProperty(ref _baseImageSeqLen, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MaxImageSeqLen
        {
            get { return _maxImageSeqLen; }
            set { SetProperty(ref _maxImageSeqLen, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public BetaScheduleType BetaSchedule
        {
            get { return _betaSchedule; }
            set { SetProperty(ref _betaSchedule, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float BetaStart
        {
            get { return _betaStart; }
            set { SetProperty(ref _betaStart, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float BetaEnd
        {
            get { return _betaEnd; }
            set { SetProperty(ref _betaEnd, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public PredictionType PredictionType
        {
            get { return _predictionType; }
            set { SetProperty(ref _predictionType, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimestepSpacingType TimestepSpacing
        {
            get { return _timestepSpacing; }
            set { SetProperty(ref _timestepSpacing, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int StepsOffset
        {
            get { return _stepsOffset; }
            set { SetProperty(ref _stepsOffset, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ClipSample
        {
            get { return _clipSample; }
            set { SetProperty(ref _clipSample, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float ClipSampleRange
        {
            get { return _clipSampleRange; }
            set { SetProperty(ref _clipSampleRange, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SampleMaxValue
        {
            get { return _sampleMaxValue; }
            set { SetProperty(ref _sampleMaxValue, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Thresholding
        {
            get { return _thresholding; }
            set { SetProperty(ref _thresholding, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float DynamicThresholdingRatio
        {
            get { return _dynamicThresholdingRatio; }
            set { SetProperty(ref _dynamicThresholdingRatio, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public VarianceType? VarianceType
        {
            get { return _varianceType; }
            set { SetProperty(ref _varianceType, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool UseKarrasSigmas
        {
            get { return _useKarrasSigmas; }
            set { SetProperty(ref _useKarrasSigmas, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool UseBetaSigmas
        {
            get { return _useBetaSigmas; }
            set { SetProperty(ref _useBetaSigmas, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool UseExponentialSigmas
        {
            get { return _useExponentialSigmas; }
            set { SetProperty(ref _useExponentialSigmas, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool UseFlowSigmas
        {
            get { return _useFlowSigmas; }
            set { SetProperty(ref _useFlowSigmas, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SigmaMin
        {
            get { return _sigmaMin; }
            set { SetProperty(ref _sigmaMin, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SigmaMax
        {
            get { return _sigmaMax; }
            set { SetProperty(ref _sigmaMax, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public FinalSigmasType FinalSigmasType
        {
            get { return _finalSigmasType; }
            set { SetProperty(ref _finalSigmasType, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public InterpolationType InterpolationType
        {
            get { return _interpolationType; }
            set { SetProperty(ref _interpolationType, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimestepType TimestepType
        {
            get { return _timestepType; }
            set { SetProperty(ref _timestepType, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool RescaleBetasZeroSNR
        {
            get { return _rescaleBetasZeroSNR; }
            set { SetProperty(ref _rescaleBetasZeroSNR, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool SetAlphaToOne
        {
            get { return _setAlphaToOne; }
            set { SetProperty(ref _setAlphaToOne, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float TimestepScaling
        {
            get { return _timestepScaling; }
            set { SetProperty(ref _timestepScaling, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Shift
        {
            get { return _shift; }
            set { SetProperty(ref _shift, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float BaseShift
        {
            get { return _baseShift; }
            set { SetProperty(ref _baseShift, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float MaxShift
        {
            get { return _maxShift; }
            set { SetProperty(ref _maxShift, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float? ShiftTerminal
        {
            get { return _shiftTerminal; }
            set { SetProperty(ref _shiftTerminal, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool UseDynamicShifting
        {
            get { return _useDynamicShifting; }
            set { SetProperty(ref _useDynamicShifting, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float FlowShift
        {
            get { return _flowShift; }
            set { SetProperty(ref _flowShift, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SNRShiftScale
        {
            get { return _sNRShiftScale; }
            set { SetProperty(ref _sNRShiftScale, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimeShiftType TimeShiftType
        {
            get { return _timeShiftType; }
            set { SetProperty(ref _timeShiftType, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Rho
        {
            get { return _rho; }
            set { SetProperty(ref _rho, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int SolverOrder
        {
            get { return _solverOrder; }
            set { SetProperty(ref _solverOrder, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public SolverType SolverType
        {
            get { return _solverType; }
            set { SetProperty(ref _solverType, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AlgorithmType AlgorithmType
        {
            get { return _algorithmType; }
            set { SetProperty(ref _algorithmType, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool LowerOrderFinal
        {
            get { return _lowerOrderFinal; }
            set { SetProperty(ref _lowerOrderFinal, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool StochasticSampling
        {
            get { return _stochasticSampling; }
            set { SetProperty(ref _stochasticSampling, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Eta
        {
            get { return _eta; }
            set { SetProperty(ref _eta, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SNoise
        {
            get { return _sNoise; }
            set { SetProperty(ref _sNoise, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool InvertSigmas
        {
            get { return _invertSigmas; }
            set { SetProperty(ref _invertSigmas, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool SkipPrkSteps
        {
            get { return _skipPrkSteps; }
            set { SetProperty(ref _skipPrkSteps, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool PredictX0
        {
            get { return _predictX0; }
            set { SetProperty(ref _predictX0, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool EulerAtFinal
        {
            get { return _eulerAtFinal; }
            set { SetProperty(ref _eulerAtFinal, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool UseLuLambdas
        {
            get { return _useLuLambdas; }
            set { SetProperty(ref _useLuLambdas, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int? NoiseSamplerSeed
        {
            get { return _noiseSamplerSeed; }
            set { SetProperty(ref _noiseSamplerSeed, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SigmaData
        {
            get { return _sigmaData; }
            set { SetProperty(ref _sigmaData, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public SigmaScheduleType SigmaScheduleType
        {
            get { return _sigmaScheduleType; }
            set { SetProperty(ref _sigmaScheduleType, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public UpscaleModeType UpscaleMode
        {
            get { return _upscaleMode; }
            set { SetProperty(ref _upscaleMode, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Stages
        {
            get { return _stages; }
            set { SetProperty(ref _stages, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Gamma
        {
            get { return _gamma; }
            set { SetProperty(ref _gamma, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int PredictorOrder
        {
            get { return _predictorOrder; }
            set { SetProperty(ref _predictorOrder, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CorrectorOrder
        {
            get { return _correctorOrder; }
            set { SetProperty(ref _correctorOrder, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<float> ScaleFactors
        {
            get { return _scaleFactors; }
            set { SetProperty(ref _scaleFactors, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<float> StageRange
        {
            get { return _stageRange; }
            set { SetProperty(ref _stageRange, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<int> DisableCorrector
        {
            get { return _disableCorrector; }
            set { SetProperty(ref _disableCorrector, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SValue
        {
            get { return _sValue; }
            set { SetProperty(ref _sValue, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Scaler
        {
            get { return _scaler; }
            set { SetProperty(ref _scaler, value); }
        }

        public static SchedulerInputOptions Create(SchedulerOptions options)
        {
            if (options is LMSOptions lmsOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = lmsOptions.Scheduler,
                    NumTrainTimesteps = lmsOptions.NumTrainTimesteps,
                    BetaEnd = lmsOptions.BetaEnd,
                    BetaSchedule = lmsOptions.BetaSchedule,
                    BetaStart = lmsOptions.BetaStart,
                    PredictionType = lmsOptions.PredictionType,
                    TimestepSpacing = lmsOptions.TimestepSpacing,
                    StepsOffset = lmsOptions.StepsOffset,
                    UseKarrasSigmas = lmsOptions.UseKarrasSigmas,
                    UseBetaSigmas = lmsOptions.UseBetaSigmas,
                    UseExponentialSigmas = lmsOptions.UseExponentialSigmas,
                };
            }
            else if (options is EulerOptions eulerOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = eulerOptions.Scheduler,
                    NumTrainTimesteps = eulerOptions.NumTrainTimesteps,
                    BetaEnd = eulerOptions.BetaEnd,
                    BetaSchedule = eulerOptions.BetaSchedule,
                    BetaStart = eulerOptions.BetaStart,
                    PredictionType = eulerOptions.PredictionType,
                    TimestepSpacing = eulerOptions.TimestepSpacing,
                    StepsOffset = eulerOptions.StepsOffset,
                    UseKarrasSigmas = eulerOptions.UseKarrasSigmas,
                    UseBetaSigmas = eulerOptions.UseBetaSigmas,
                    UseExponentialSigmas = eulerOptions.UseExponentialSigmas,
                    SigmaMax = eulerOptions.SigmaMax ?? 0,
                    SigmaMin = eulerOptions.SigmaMin ?? 0,
                    FinalSigmasType = eulerOptions.FinalSigmasType,
                    InterpolationType = eulerOptions.InterpolationType,
                    TimestepType = eulerOptions.TimestepType,
                    RescaleBetasZeroSNR = eulerOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is EulerAncestralOptions eulerAncestralOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = eulerAncestralOptions.Scheduler,
                    NumTrainTimesteps = eulerAncestralOptions.NumTrainTimesteps,
                    BetaEnd = eulerAncestralOptions.BetaEnd,
                    BetaSchedule = eulerAncestralOptions.BetaSchedule,
                    BetaStart = eulerAncestralOptions.BetaStart,
                    PredictionType = eulerAncestralOptions.PredictionType,
                    TimestepSpacing = eulerAncestralOptions.TimestepSpacing,
                    StepsOffset = eulerAncestralOptions.StepsOffset,
                    RescaleBetasZeroSNR = eulerAncestralOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is DDPMOptions ddpmOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = ddpmOptions.Scheduler,
                    NumTrainTimesteps = ddpmOptions.NumTrainTimesteps,
                    BetaEnd = ddpmOptions.BetaEnd,
                    BetaSchedule = ddpmOptions.BetaSchedule,
                    BetaStart = ddpmOptions.BetaStart,
                    PredictionType = ddpmOptions.PredictionType,
                    TimestepSpacing = ddpmOptions.TimestepSpacing,
                    StepsOffset = ddpmOptions.StepsOffset,
                    ClipSample = ddpmOptions.ClipSample,
                    ClipSampleRange = ddpmOptions.ClipSampleRange,
                    SampleMaxValue = ddpmOptions.SampleMaxValue,
                    Thresholding = ddpmOptions.Thresholding,
                    DynamicThresholdingRatio = ddpmOptions.DynamicThresholdingRatio,
                    VarianceType = ddpmOptions.VarianceType,
                    RescaleBetasZeroSNR = ddpmOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is DDIMOptions ddimOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = ddimOptions.Scheduler,
                    NumTrainTimesteps = ddimOptions.NumTrainTimesteps,
                    BetaEnd = ddimOptions.BetaEnd,
                    BetaSchedule = ddimOptions.BetaSchedule,
                    BetaStart = ddimOptions.BetaStart,
                    PredictionType = ddimOptions.PredictionType,
                    TimestepSpacing = ddimOptions.TimestepSpacing,
                    StepsOffset = ddimOptions.StepsOffset,
                    ClipSample = ddimOptions.ClipSample,
                    ClipSampleRange = ddimOptions.ClipSampleRange,
                    SampleMaxValue = ddimOptions.SampleMaxValue,
                    Thresholding = ddimOptions.Thresholding,
                    DynamicThresholdingRatio = ddimOptions.DynamicThresholdingRatio,
                    SetAlphaToOne = ddimOptions.SetAlphaToOne,
                    RescaleBetasZeroSNR = ddimOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is KDPM2Options kdpm2Options)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = kdpm2Options.Scheduler,
                    NumTrainTimesteps = kdpm2Options.NumTrainTimesteps,
                    BetaEnd = kdpm2Options.BetaEnd,
                    BetaSchedule = kdpm2Options.BetaSchedule,
                    BetaStart = kdpm2Options.BetaStart,
                    PredictionType = kdpm2Options.PredictionType,
                    TimestepSpacing = kdpm2Options.TimestepSpacing,
                    StepsOffset = kdpm2Options.StepsOffset,
                    UseKarrasSigmas = kdpm2Options.UseKarrasSigmas,
                    UseBetaSigmas = kdpm2Options.UseBetaSigmas,
                    UseExponentialSigmas = kdpm2Options.UseExponentialSigmas,
                };
            }
            else if (options is KDPM2AncestralOptions kdpm2AncestralOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = kdpm2AncestralOptions.Scheduler,
                    NumTrainTimesteps = kdpm2AncestralOptions.NumTrainTimesteps,
                    BetaEnd = kdpm2AncestralOptions.BetaEnd,
                    BetaSchedule = kdpm2AncestralOptions.BetaSchedule,
                    BetaStart = kdpm2AncestralOptions.BetaStart,
                    PredictionType = kdpm2AncestralOptions.PredictionType,
                    TimestepSpacing = kdpm2AncestralOptions.TimestepSpacing,
                    StepsOffset = kdpm2AncestralOptions.StepsOffset,
                    UseKarrasSigmas = kdpm2AncestralOptions.UseKarrasSigmas,
                    UseBetaSigmas = kdpm2AncestralOptions.UseBetaSigmas,
                    UseExponentialSigmas = kdpm2AncestralOptions.UseExponentialSigmas,
                };
            }
            else if (options is DDPMWuerstchenOptions ddpmWuerstchenOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = ddpmWuerstchenOptions.Scheduler,
                    SValue = ddpmWuerstchenOptions.S,
                    Scaler = ddpmWuerstchenOptions.Scaler,
                };
            }
            else if (options is LCMOptions lcmOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = lcmOptions.Scheduler,
                    NumTrainTimesteps = lcmOptions.NumTrainTimesteps,
                    OriginalInferenceSteps = lcmOptions.OriginalInferenceSteps,
                    BetaEnd = lcmOptions.BetaEnd,
                    BetaSchedule = lcmOptions.BetaSchedule,
                    BetaStart = lcmOptions.BetaStart,
                    PredictionType = lcmOptions.PredictionType,
                    TimestepSpacing = lcmOptions.TimestepSpacing,
                    StepsOffset = lcmOptions.StepsOffset,
                    ClipSample = lcmOptions.ClipSample,
                    ClipSampleRange = lcmOptions.ClipSampleRange,
                    SampleMaxValue = lcmOptions.SampleMaxValue,
                    Thresholding = lcmOptions.Thresholding,
                    DynamicThresholdingRatio = lcmOptions.DynamicThresholdingRatio,
                    SetAlphaToOne = lcmOptions.SetAlphaToOne,
                    TimestepScaling = lcmOptions.TimestepScaling,
                    RescaleBetasZeroSNR = lcmOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is FlowMatchEulerOptions flowMatchEulerOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = flowMatchEulerOptions.Scheduler,
                    NumTrainTimesteps = flowMatchEulerOptions.NumTrainTimesteps,
                    Shift = flowMatchEulerOptions.Shift,
                    BaseShift = flowMatchEulerOptions.BaseShift ?? 0,
                    MaxShift = flowMatchEulerOptions.MaxShift ?? 0,
                    ShiftTerminal = flowMatchEulerOptions.ShiftTerminal,
                    TimeShiftType = flowMatchEulerOptions.TimeShiftType,
                    UseDynamicShifting = flowMatchEulerOptions.UseDynamicShifting,
                    BaseImageSeqLen = flowMatchEulerOptions.BaseImageSeqLen,
                    MaxImageSeqLen = flowMatchEulerOptions.MaxImageSeqLen,
                    InvertSigmas = flowMatchEulerOptions.InvertSigmas,
                    StochasticSampling = flowMatchEulerOptions.StochasticSampling,
                    UseKarrasSigmas = flowMatchEulerOptions.UseKarrasSigmas,
                    UseBetaSigmas = flowMatchEulerOptions.UseBetaSigmas,
                    UseExponentialSigmas = flowMatchEulerOptions.UseExponentialSigmas,
                };
            }
            else if (options is FlowMatchHeunOptions flowMatchHeunOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = flowMatchHeunOptions.Scheduler,
                    NumTrainTimesteps = flowMatchHeunOptions.NumTrainTimesteps,
                    Shift = flowMatchHeunOptions.Shift,
                };
            }
            else if (options is PNDMOptions pndmOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = pndmOptions.Scheduler,
                    NumTrainTimesteps = pndmOptions.NumTrainTimesteps,
                    BetaEnd = pndmOptions.BetaEnd,
                    BetaSchedule = pndmOptions.BetaSchedule,
                    BetaStart = pndmOptions.BetaStart,
                    PredictionType = pndmOptions.PredictionType,
                    TimestepSpacing = pndmOptions.TimestepSpacing,
                    StepsOffset = pndmOptions.StepsOffset,
                    SetAlphaToOne = pndmOptions.SetAlphaToOne,
                    SkipPrkSteps = pndmOptions.SkipPrkSteps,
                };
            }
            else if (options is HeunOptions heunOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = heunOptions.Scheduler,
                    NumTrainTimesteps = heunOptions.NumTrainTimesteps,
                    BetaEnd = heunOptions.BetaEnd,
                    BetaSchedule = heunOptions.BetaSchedule,
                    BetaStart = heunOptions.BetaStart,
                    PredictionType = heunOptions.PredictionType,
                    TimestepSpacing = heunOptions.TimestepSpacing,
                    StepsOffset = heunOptions.StepsOffset,
                    ClipSample = heunOptions.ClipSample,
                    ClipSampleRange = heunOptions.ClipSampleRange,
                    UseKarrasSigmas = heunOptions.UseKarrasSigmas,
                    UseBetaSigmas = heunOptions.UseBetaSigmas,
                    UseExponentialSigmas = heunOptions.UseExponentialSigmas,
                };
            }
            else if (options is UniPCMultistepOptions unipcMultistepOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = unipcMultistepOptions.Scheduler,
                    NumTrainTimesteps = unipcMultistepOptions.NumTrainTimesteps,
                    BetaEnd = unipcMultistepOptions.BetaEnd,
                    BetaSchedule = unipcMultistepOptions.BetaSchedule,
                    BetaStart = unipcMultistepOptions.BetaStart,
                    PredictionType = unipcMultistepOptions.PredictionType,
                    TimestepSpacing = unipcMultistepOptions.TimestepSpacing,
                    StepsOffset = unipcMultistepOptions.StepsOffset,
                    Thresholding = unipcMultistepOptions.Thresholding,
                    DynamicThresholdingRatio = unipcMultistepOptions.DynamicThresholdingRatio,
                    SampleMaxValue = unipcMultistepOptions.SampleMaxValue,
                    SigmaMin = unipcMultistepOptions.SigmaMin ?? 0,
                    SigmaMax = unipcMultistepOptions.SigmaMax ?? 0,
                    FinalSigmasType = unipcMultistepOptions.FinalSigmasType,
                    SolverType = unipcMultistepOptions.SolverType,
                    SolverOrder = unipcMultistepOptions.SolverOrder,
                    LowerOrderFinal = unipcMultistepOptions.LowerOrderFinal,
                    ShiftTerminal = unipcMultistepOptions.ShiftTerminal,
                    TimeShiftType = unipcMultistepOptions.TimeShiftType,
                    UseDynamicShifting = unipcMultistepOptions.UseDynamicShifting,
                    FlowShift = unipcMultistepOptions.FlowShift,
                    PredictX0 = unipcMultistepOptions.PredictX0,
                    UseFlowSigmas = unipcMultistepOptions.UseFlowSigmas,
                    UseKarrasSigmas = unipcMultistepOptions.UseKarrasSigmas,
                    UseBetaSigmas = unipcMultistepOptions.UseBetaSigmas,
                    UseExponentialSigmas = unipcMultistepOptions.UseExponentialSigmas,
                    RescaleBetasZeroSNR = unipcMultistepOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is DPMSolverMultistepOptions dpmSolverMultistepOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = dpmSolverMultistepOptions.Scheduler,
                    NumTrainTimesteps = dpmSolverMultistepOptions.NumTrainTimesteps,
                    BetaEnd = dpmSolverMultistepOptions.BetaEnd,
                    BetaSchedule = dpmSolverMultistepOptions.BetaSchedule,
                    BetaStart = dpmSolverMultistepOptions.BetaStart,
                    PredictionType = dpmSolverMultistepOptions.PredictionType,
                    TimestepSpacing = dpmSolverMultistepOptions.TimestepSpacing,
                    StepsOffset = dpmSolverMultistepOptions.StepsOffset,
                    Thresholding = dpmSolverMultistepOptions.Thresholding,
                    DynamicThresholdingRatio = dpmSolverMultistepOptions.DynamicThresholdingRatio,
                    SampleMaxValue = dpmSolverMultistepOptions.SampleMaxValue,
                    SolverOrder = dpmSolverMultistepOptions.SolverOrder,
                    SolverType = dpmSolverMultistepOptions.SolverType,
                    LowerOrderFinal = dpmSolverMultistepOptions.LowerOrderFinal,
                    FlowShift = dpmSolverMultistepOptions.FlowShift,
                    TimeShiftType = dpmSolverMultistepOptions.TimeShiftType,
                    FinalSigmasType = dpmSolverMultistepOptions.FinalSigmasType,
                    UseDynamicShifting = dpmSolverMultistepOptions.UseDynamicShifting,
                    UseFlowSigmas = dpmSolverMultistepOptions.UseFlowSigmas,
                    EulerAtFinal = dpmSolverMultistepOptions.EulerAtFinal,
                    AlgorithmType = dpmSolverMultistepOptions.AlgorithmType,
                    UseLuLambdas = dpmSolverMultistepOptions.UseLuLambdas,
                    UseKarrasSigmas = dpmSolverMultistepOptions.UseKarrasSigmas,
                    UseBetaSigmas = dpmSolverMultistepOptions.UseBetaSigmas,
                    UseExponentialSigmas = dpmSolverMultistepOptions.UseExponentialSigmas,
                    RescaleBetasZeroSNR = dpmSolverMultistepOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is DPMSolverSinglestepOptions dpmSolverSinglestepOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = dpmSolverSinglestepOptions.Scheduler,
                    NumTrainTimesteps = dpmSolverSinglestepOptions.NumTrainTimesteps,
                    BetaEnd = dpmSolverSinglestepOptions.BetaEnd,
                    BetaSchedule = dpmSolverSinglestepOptions.BetaSchedule,
                    BetaStart = dpmSolverSinglestepOptions.BetaStart,
                    PredictionType = dpmSolverSinglestepOptions.PredictionType,
                    TimestepSpacing = dpmSolverSinglestepOptions.TimestepSpacing,
                    StepsOffset = dpmSolverSinglestepOptions.StepsOffset,
                    Thresholding = dpmSolverSinglestepOptions.Thresholding,
                    DynamicThresholdingRatio = dpmSolverSinglestepOptions.DynamicThresholdingRatio,
                    SampleMaxValue = dpmSolverSinglestepOptions.SampleMaxValue,
                    SolverOrder = dpmSolverSinglestepOptions.SolverOrder,
                    SolverType = dpmSolverSinglestepOptions.SolverType,
                    LowerOrderFinal = dpmSolverSinglestepOptions.LowerOrderFinal,
                    FlowShift = dpmSolverSinglestepOptions.FlowShift,
                    VarianceType = dpmSolverSinglestepOptions.VarianceType,
                    AlgorithmType = dpmSolverSinglestepOptions.AlgorithmType,
                    FinalSigmasType = dpmSolverSinglestepOptions.FinalSigmasType,
                    UseFlowSigmas = dpmSolverSinglestepOptions.UseFlowSigmas,
                    UseKarrasSigmas = dpmSolverSinglestepOptions.UseKarrasSigmas,
                    UseBetaSigmas = dpmSolverSinglestepOptions.UseBetaSigmas,
                    UseExponentialSigmas = dpmSolverSinglestepOptions.UseExponentialSigmas,
                };
            }
            else if (options is DPMSolverSDEOptions dpmSolverSDEOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = dpmSolverSDEOptions.Scheduler,
                    NumTrainTimesteps = dpmSolverSDEOptions.NumTrainTimesteps,
                    BetaEnd = dpmSolverSDEOptions.BetaEnd,
                    BetaSchedule = dpmSolverSDEOptions.BetaSchedule,
                    BetaStart = dpmSolverSDEOptions.BetaStart,
                    PredictionType = dpmSolverSDEOptions.PredictionType,
                    TimestepSpacing = dpmSolverSDEOptions.TimestepSpacing,
                    StepsOffset = dpmSolverSDEOptions.StepsOffset,
                    UseKarrasSigmas = dpmSolverSDEOptions.UseKarrasSigmas,
                    UseBetaSigmas = dpmSolverSDEOptions.UseBetaSigmas,
                    UseExponentialSigmas = dpmSolverSDEOptions.UseExponentialSigmas,
                    NoiseSamplerSeed = dpmSolverSDEOptions.NoiseSamplerSeed,
                };
            }
            else if (options is DEISMultistepOptions deisMultistepOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = deisMultistepOptions.Scheduler,
                    NumTrainTimesteps = deisMultistepOptions.NumTrainTimesteps,
                    BetaEnd = deisMultistepOptions.BetaEnd,
                    BetaSchedule = deisMultistepOptions.BetaSchedule,
                    BetaStart = deisMultistepOptions.BetaStart,
                    PredictionType = deisMultistepOptions.PredictionType,
                    TimestepSpacing = deisMultistepOptions.TimestepSpacing,
                    StepsOffset = deisMultistepOptions.StepsOffset,
                    Thresholding = deisMultistepOptions.Thresholding,
                    DynamicThresholdingRatio = deisMultistepOptions.DynamicThresholdingRatio,
                    SampleMaxValue = deisMultistepOptions.SampleMaxValue,
                    SolverOrder = deisMultistepOptions.SolverOrder,
                    SolverType = deisMultistepOptions.SolverType,
                    LowerOrderFinal = deisMultistepOptions.LowerOrderFinal,
                    FlowShift = deisMultistepOptions.FlowShift,
                    TimeShiftType = deisMultistepOptions.TimeShiftType,
                    AlgorithmType = deisMultistepOptions.AlgorithmType,
                    UseDynamicShifting = deisMultistepOptions.UseDynamicShifting,
                    UseFlowSigmas = deisMultistepOptions.UseFlowSigmas,
                    UseKarrasSigmas = deisMultistepOptions.UseKarrasSigmas,
                    UseBetaSigmas = deisMultistepOptions.UseBetaSigmas,
                    UseExponentialSigmas = deisMultistepOptions.UseExponentialSigmas,
                };
            }
            else if (options is EDMEulerOptions edmEulerOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = edmEulerOptions.Scheduler,
                    NumTrainTimesteps = edmEulerOptions.NumTrainTimesteps,
                    PredictionType = edmEulerOptions.PredictionType,
                    SigmaMax = edmEulerOptions.SigmaMax,
                    SigmaMin = edmEulerOptions.SigmaMin,
                    FinalSigmasType = edmEulerOptions.FinalSigmasType,
                    Rho = edmEulerOptions.Rho,
                    SigmaData = edmEulerOptions.SigmaData,
                    SigmaScheduleType = edmEulerOptions.SigmaScheduleType,
                };
            }
            else if (options is EDMDPMSolverMultistepOptions edmDPMSolverMultistep)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = edmDPMSolverMultistep.Scheduler,
                    NumTrainTimesteps = edmDPMSolverMultistep.NumTrainTimesteps,
                    PredictionType = edmDPMSolverMultistep.PredictionType,
                    AlgorithmType = edmDPMSolverMultistep.AlgorithmType,
                    EulerAtFinal = edmDPMSolverMultistep.EulerAtFinal,
                    SigmaMax = edmDPMSolverMultistep.SigmaMax,
                    SigmaMin = edmDPMSolverMultistep.SigmaMin,
                    FinalSigmasType = edmDPMSolverMultistep.FinalSigmasType,
                    Rho = edmDPMSolverMultistep.Rho,
                    SigmaData = edmDPMSolverMultistep.SigmaData,
                    SigmaScheduleType = edmDPMSolverMultistep.SigmaScheduleType,
                    Thresholding = edmDPMSolverMultistep.Thresholding,
                    DynamicThresholdingRatio = edmDPMSolverMultistep.DynamicThresholdingRatio,
                    SampleMaxValue = edmDPMSolverMultistep.SampleMaxValue,
                    SolverOrder = edmDPMSolverMultistep.SolverOrder,
                    SolverType = edmDPMSolverMultistep.SolverType,
                    LowerOrderFinal = edmDPMSolverMultistep.LowerOrderFinal,
                };
            }
            else if (options is FlowMatchLCMOptions flowMatchLCMOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = flowMatchLCMOptions.Scheduler,
                    NumTrainTimesteps = flowMatchLCMOptions.NumTrainTimesteps,
                    Shift = flowMatchLCMOptions.Shift,
                    BaseShift = flowMatchLCMOptions.BaseShift ?? 0,
                    MaxShift = flowMatchLCMOptions.MaxShift ?? 0,
                    ShiftTerminal = flowMatchLCMOptions.ShiftTerminal,
                    TimeShiftType = flowMatchLCMOptions.TimeShiftType,
                    UpscaleMode = flowMatchLCMOptions.UpscaleMode,
                    UseDynamicShifting = flowMatchLCMOptions.UseDynamicShifting,
                    InvertSigmas = flowMatchLCMOptions.InvertSigmas,
                    BaseImageSeqLen = flowMatchLCMOptions.BaseImageSeqLen,     // TODO
                    MaxImageSeqLen = flowMatchLCMOptions.MaxImageSeqLen,       // TODO
                    ScaleFactors = flowMatchLCMOptions.ScaleFactors,           // TODO
                    UseKarrasSigmas = flowMatchLCMOptions.UseKarrasSigmas,
                    UseBetaSigmas = flowMatchLCMOptions.UseBetaSigmas,
                    UseExponentialSigmas = flowMatchLCMOptions.UseExponentialSigmas,
                };
            }
            else if (options is IPNDMOptions ipndmOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = ipndmOptions.Scheduler,
                    NumTrainTimesteps = ipndmOptions.NumTrainTimesteps,
                };
            }
            else if (options is CogVideoXDDIMOptions cogDDIMOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = cogDDIMOptions.Scheduler,
                    NumTrainTimesteps = cogDDIMOptions.NumTrainTimesteps,
                    BetaEnd = cogDDIMOptions.BetaEnd,
                    BetaSchedule = cogDDIMOptions.BetaSchedule,
                    BetaStart = cogDDIMOptions.BetaStart,
                    PredictionType = cogDDIMOptions.PredictionType,
                    TimestepSpacing = cogDDIMOptions.TimestepSpacing,
                    StepsOffset = cogDDIMOptions.StepsOffset,
                    ClipSample = cogDDIMOptions.ClipSample,
                    ClipSampleRange = cogDDIMOptions.ClipSampleRange,
                    SampleMaxValue = cogDDIMOptions.SampleMaxValue,
                    SetAlphaToOne = cogDDIMOptions.SetAlphaToOne,
                    SNRShiftScale = cogDDIMOptions.SNRShiftScale,
                    RescaleBetasZeroSNR = cogDDIMOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is CogVideoXDPMOptions cogDPMOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = cogDPMOptions.Scheduler,
                    NumTrainTimesteps = cogDPMOptions.NumTrainTimesteps,
                    BetaEnd = cogDPMOptions.BetaEnd,
                    BetaSchedule = cogDPMOptions.BetaSchedule,
                    BetaStart = cogDPMOptions.BetaStart,
                    PredictionType = cogDPMOptions.PredictionType,
                    TimestepSpacing = cogDPMOptions.TimestepSpacing,
                    StepsOffset = cogDPMOptions.StepsOffset,
                    ClipSample = cogDPMOptions.ClipSample,
                    ClipSampleRange = cogDPMOptions.ClipSampleRange,
                    SampleMaxValue = cogDPMOptions.SampleMaxValue,
                    SetAlphaToOne = cogDPMOptions.SetAlphaToOne,
                    SNRShiftScale = cogDPMOptions.SNRShiftScale,
                    RescaleBetasZeroSNR = cogDPMOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is HeliosOptions heliosOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = heliosOptions.Scheduler,
                    NumTrainTimesteps = heliosOptions.NumTrainTimesteps,
                    PredictionType = heliosOptions.PredictionType,
                    Shift = heliosOptions.Shift,
                    Gamma = heliosOptions.Gamma,
                    SolverOrder = heliosOptions.SolverOrder,
                    SolverType = heliosOptions.SolverType,
                    LowerOrderFinal = heliosOptions.LowerOrderFinal,
                    TimeShiftType = heliosOptions.TimeShiftType,
                    UseDynamicShifting = heliosOptions.UseDynamicShifting,
                    UseFlowSigmas = heliosOptions.UseFlowSigmas,
                    PredictX0 = heliosOptions.PredictX0,
                    Thresholding = heliosOptions.Thresholding,
                    Stages = heliosOptions.Stages,                            // TODO
                    StageRange = heliosOptions.StageRange,                    // TODO
                    DisableCorrector = heliosOptions.DisableCorrector,        // TODO
                };
            }
            else if (options is HeliosDMDOptions heliosDMDOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = heliosDMDOptions.Scheduler,
                    NumTrainTimesteps = heliosDMDOptions.NumTrainTimesteps,
                    PredictionType = heliosDMDOptions.PredictionType,
                    Shift = heliosDMDOptions.Shift,
                    Gamma = heliosDMDOptions.Gamma,
                    TimeShiftType = heliosDMDOptions.TimeShiftType,
                    UseDynamicShifting = heliosDMDOptions.UseDynamicShifting,
                    UseFlowSigmas = heliosDMDOptions.UseFlowSigmas,
                    Stages = heliosDMDOptions.Stages,                            // TODO
                    StageRange = heliosDMDOptions.StageRange,                    // TODO
                };
            }
            else if (options is TCDOptions tcdOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = tcdOptions.Scheduler,
                    NumTrainTimesteps = tcdOptions.NumTrainTimesteps,
                    BetaEnd = tcdOptions.BetaEnd,
                    BetaSchedule = tcdOptions.BetaSchedule,
                    BetaStart = tcdOptions.BetaStart,
                    PredictionType = tcdOptions.PredictionType,
                    TimestepSpacing = tcdOptions.TimestepSpacing,
                    StepsOffset = tcdOptions.StepsOffset,
                    Thresholding = tcdOptions.Thresholding,
                    DynamicThresholdingRatio = tcdOptions.DynamicThresholdingRatio,
                    TimestepScaling = tcdOptions.TimestepScaling,
                    ClipSample = tcdOptions.ClipSample,
                    ClipSampleRange = tcdOptions.ClipSampleRange,
                    SampleMaxValue = tcdOptions.SampleMaxValue,
                    OriginalInferenceSteps = tcdOptions.OriginalInferenceSteps,
                    SetAlphaToOne = tcdOptions.SetAlphaToOne,
                    RescaleBetasZeroSNR = tcdOptions.RescaleBetasZeroSNR, // TODO
                };
            }
            else if (options is SCMOptions scmOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = scmOptions.Scheduler,
                    NumTrainTimesteps = scmOptions.NumTrainTimesteps,
                    PredictionType = scmOptions.PredictionType,
                    SigmaData = scmOptions.SigmaData,
                };
            }
            else if (options is SASolverOptions saSolverOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = saSolverOptions.Scheduler,
                    NumTrainTimesteps = saSolverOptions.NumTrainTimesteps,
                    BetaEnd = saSolverOptions.BetaEnd,
                    BetaSchedule = saSolverOptions.BetaSchedule,
                    BetaStart = saSolverOptions.BetaStart,
                    PredictionType = saSolverOptions.PredictionType,
                    TimestepSpacing = saSolverOptions.TimestepSpacing,
                    StepsOffset = saSolverOptions.StepsOffset,
                    Thresholding = saSolverOptions.Thresholding,
                    DynamicThresholdingRatio = saSolverOptions.DynamicThresholdingRatio,
                    SampleMaxValue = saSolverOptions.SampleMaxValue,
                    FlowShift = saSolverOptions.FlowShift,
                    AlgorithmType = saSolverOptions.AlgorithmType,
                    VarianceType = saSolverOptions.VarianceType,
                    UseFlowSigmas = saSolverOptions.UseFlowSigmas,
                    LowerOrderFinal = saSolverOptions.LowerOrderFinal,
                    PredictorOrder = saSolverOptions.PredictorOrder,
                    CorrectorOrder = saSolverOptions.CorrectorOrder,
                    UseBetaSigmas = saSolverOptions.UseBetaSigmas,
                    UseExponentialSigmas = saSolverOptions.UseExponentialSigmas,
                    UseKarrasSigmas = saSolverOptions.UseKarrasSigmas,
                };
            }
            else if (options is LTXEulerAncestralRFOptions ltxEulerAncestralRFOptions)
            {
                return new SchedulerInputOptions
                {
                    Scheduler = ltxEulerAncestralRFOptions.Scheduler,
                    NumTrainTimesteps = ltxEulerAncestralRFOptions.NumTrainTimesteps,
                    Eta = ltxEulerAncestralRFOptions.Eta,
                    SNoise = ltxEulerAncestralRFOptions.SNoise,
                };
            }

            throw new NotImplementedException();
        }


        public SchedulerOptions ToOptions()
        {
            return Scheduler switch
            {
                SchedulerType.LMS => new LMSOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                },
                SchedulerType.Euler => new EulerOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                    SigmaMax = SigmaMax > 0 ? SigmaMax : null,
                    SigmaMin = SigmaMin > 0 ? SigmaMin : null,
                    FinalSigmasType = FinalSigmasType,
                    InterpolationType = InterpolationType,
                    TimestepType = TimestepType,
                    RescaleBetasZeroSNR = RescaleBetasZeroSNR,
                },
                SchedulerType.EulerAncestral => new EulerAncestralOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    RescaleBetasZeroSNR = RescaleBetasZeroSNR,
                },
                SchedulerType.DDPM => new DDPMOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    ClipSample = ClipSample,
                    ClipSampleRange = ClipSampleRange,
                    SampleMaxValue = SampleMaxValue,
                    Thresholding = Thresholding,
                    DynamicThresholdingRatio = DynamicThresholdingRatio,
                    VarianceType = VarianceType ?? TensorStack.Python.Scheduler.VarianceType.FixedSmall,
                    RescaleBetasZeroSNR = RescaleBetasZeroSNR,
                },
                SchedulerType.DDIM => new DDIMOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    ClipSample = ClipSample,
                    ClipSampleRange = ClipSampleRange,
                    SampleMaxValue = SampleMaxValue,
                    Thresholding = Thresholding,
                    DynamicThresholdingRatio = DynamicThresholdingRatio,
                    SetAlphaToOne = SetAlphaToOne,
                    RescaleBetasZeroSNR = RescaleBetasZeroSNR,
                },
                SchedulerType.KDPM2 => new KDPM2Options
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                },
                SchedulerType.KDPM2Ancestral => new KDPM2AncestralOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                },
                SchedulerType.DDPMWuerstchen => new DDPMWuerstchenOptions
                {
                    S = SValue,
                    Scaler = Scaler,
                },
                SchedulerType.LCM => new LCMOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    OriginalInferenceSteps = OriginalInferenceSteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    ClipSample = ClipSample,
                    ClipSampleRange = ClipSampleRange,
                    SampleMaxValue = SampleMaxValue,
                    Thresholding = Thresholding,
                    DynamicThresholdingRatio = DynamicThresholdingRatio,
                    SetAlphaToOne = SetAlphaToOne,
                    TimestepScaling = TimestepScaling,
                    RescaleBetasZeroSNR = RescaleBetasZeroSNR,
                },
                SchedulerType.FlowMatchEuler => new FlowMatchEulerOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    Shift = Shift,
                    BaseShift = BaseShift,
                    MaxShift = MaxShift,
                    ShiftTerminal = ShiftTerminal > 0 ? ShiftTerminal : null,
                    TimeShiftType = TimeShiftType,
                    UseDynamicShifting = UseDynamicShifting,
                    BaseImageSeqLen = BaseImageSeqLen,
                    MaxImageSeqLen = MaxImageSeqLen,
                    InvertSigmas = InvertSigmas,
                    StochasticSampling = StochasticSampling,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                },
                SchedulerType.FlowMatchHeun => new FlowMatchHeunOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    Shift = Shift,
                },
                SchedulerType.PNDM => new PNDMOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    SetAlphaToOne = SetAlphaToOne,
                    SkipPrkSteps = SkipPrkSteps,
                },
                SchedulerType.Heun => new HeunOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    ClipSample = ClipSample,
                    ClipSampleRange = ClipSampleRange,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                },
                SchedulerType.UniPCMultistep => new UniPCMultistepOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    Thresholding = Thresholding,
                    DynamicThresholdingRatio = DynamicThresholdingRatio,
                    SampleMaxValue = SampleMaxValue,
                    SigmaMin = SigmaMin > 0 ? SigmaMin : null,
                    SigmaMax = SigmaMax > 0 ? SigmaMax : null,
                    FinalSigmasType = FinalSigmasType,
                    SolverType = SolverType,
                    SolverOrder = SolverOrder,
                    LowerOrderFinal = LowerOrderFinal,
                    ShiftTerminal = ShiftTerminal > 0 ? ShiftTerminal : null,
                    TimeShiftType = TimeShiftType,
                    UseDynamicShifting = UseDynamicShifting,
                    FlowShift = FlowShift,
                    PredictX0 = PredictX0,
                    UseFlowSigmas = UseFlowSigmas,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                    RescaleBetasZeroSNR = RescaleBetasZeroSNR,
                },
                SchedulerType.DPMSolverMultistep => new DPMSolverMultistepOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    Thresholding = Thresholding,
                    DynamicThresholdingRatio = DynamicThresholdingRatio,
                    SampleMaxValue = SampleMaxValue,
                    SolverOrder = SolverOrder,
                    SolverType = SolverType,
                    LowerOrderFinal = LowerOrderFinal,
                    FlowShift = FlowShift,
                    TimeShiftType = TimeShiftType,
                    FinalSigmasType = FinalSigmasType,
                    UseDynamicShifting = UseDynamicShifting,
                    UseFlowSigmas = UseFlowSigmas,
                    EulerAtFinal = EulerAtFinal,
                    AlgorithmType = AlgorithmType,
                    UseLuLambdas = UseLuLambdas,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                    RescaleBetasZeroSNR = RescaleBetasZeroSNR,
                },
                SchedulerType.DPMSolverSinglestep => new DPMSolverSinglestepOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    Thresholding = Thresholding,
                    DynamicThresholdingRatio = DynamicThresholdingRatio,
                    SampleMaxValue = SampleMaxValue,
                    SolverOrder = SolverOrder,
                    SolverType = SolverType,
                    LowerOrderFinal = LowerOrderFinal,
                    FlowShift = FlowShift,
                    VarianceType = VarianceType,
                    AlgorithmType = AlgorithmType,
                    FinalSigmasType = FinalSigmasType,
                    UseFlowSigmas = UseFlowSigmas,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                },
                SchedulerType.DPMSolverSDE => new DPMSolverSDEOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                    NoiseSamplerSeed = NoiseSamplerSeed,
                },
                SchedulerType.DEISMultistep => new DEISMultistepOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    Thresholding = Thresholding,
                    DynamicThresholdingRatio = DynamicThresholdingRatio,
                    SampleMaxValue = SampleMaxValue,
                    SolverOrder = SolverOrder,
                    SolverType = SolverType,
                    LowerOrderFinal = LowerOrderFinal,
                    FlowShift = FlowShift,
                    TimeShiftType = TimeShiftType,
                    AlgorithmType = AlgorithmType,
                    UseDynamicShifting = UseDynamicShifting,
                    UseFlowSigmas = UseFlowSigmas,
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                },
                SchedulerType.EDMEuler => new EDMEulerOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    PredictionType = PredictionType,
                    SigmaMax = SigmaMax,
                    SigmaMin = SigmaMin,
                    FinalSigmasType = FinalSigmasType,
                    Rho = Rho,
                    SigmaData = SigmaData,
                    SigmaScheduleType = SigmaScheduleType,
                },
                SchedulerType.EDMDPMSolverMultistep => new EDMDPMSolverMultistepOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    PredictionType = PredictionType,
                    AlgorithmType = AlgorithmType,
                    EulerAtFinal = EulerAtFinal,
                    SigmaMax = SigmaMax,
                    SigmaMin = SigmaMin,
                    FinalSigmasType = FinalSigmasType,
                    Rho = Rho,
                    SigmaData = SigmaData,
                    SigmaScheduleType = SigmaScheduleType,
                    Thresholding = Thresholding,
                    DynamicThresholdingRatio = DynamicThresholdingRatio,
                    SampleMaxValue = SampleMaxValue,
                    SolverOrder = SolverOrder,
                    SolverType = SolverType,
                    LowerOrderFinal = LowerOrderFinal,
                },
                SchedulerType.FlowMatchLCM => new FlowMatchLCMOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    Shift = Shift,
                    BaseShift = BaseShift,
                    MaxShift = MaxShift,
                    ShiftTerminal = ShiftTerminal > 0 ? ShiftTerminal : null,
                    TimeShiftType = TimeShiftType,
                    UpscaleMode = UpscaleMode,
                    UseDynamicShifting = UseDynamicShifting,
                    InvertSigmas = InvertSigmas,
                    BaseImageSeqLen = BaseImageSeqLen,     // TODO
                    MaxImageSeqLen = MaxImageSeqLen,       // TODO
                    ScaleFactors = ScaleFactors,           // TODO
                    UseKarrasSigmas = UseKarrasSigmas,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                },
                SchedulerType.IPNDM => new IPNDMOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                },
                SchedulerType.CogVideoXDDIM => new CogVideoXDDIMOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    ClipSample = ClipSample,
                    ClipSampleRange = ClipSampleRange,
                    SampleMaxValue = SampleMaxValue,
                    SetAlphaToOne = SetAlphaToOne,
                    SNRShiftScale = SNRShiftScale,
                    RescaleBetasZeroSNR = RescaleBetasZeroSNR,
                },
                SchedulerType.CogVideoXDPM => new CogVideoXDPMOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    ClipSample = ClipSample,
                    ClipSampleRange = ClipSampleRange,
                    SampleMaxValue = SampleMaxValue,
                    SetAlphaToOne = SetAlphaToOne,
                    SNRShiftScale = SNRShiftScale,
                    RescaleBetasZeroSNR = RescaleBetasZeroSNR,
                },
                SchedulerType.Helios => new HeliosOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    PredictionType = PredictionType,
                    Shift = Shift,
                    Gamma = Gamma,
                    SolverOrder = SolverOrder,
                    SolverType = SolverType,
                    LowerOrderFinal = LowerOrderFinal,
                    TimeShiftType = TimeShiftType,
                    UseDynamicShifting = UseDynamicShifting,
                    UseFlowSigmas = UseFlowSigmas,
                    PredictX0 = PredictX0,
                    Thresholding = Thresholding,
                    Stages = Stages,                            // TODO
                    StageRange = StageRange,                    // TODO
                    DisableCorrector = DisableCorrector,        // TODO
                },
                SchedulerType.HeliosDMD => new HeliosDMDOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    PredictionType = PredictionType,
                    Shift = Shift,
                    Gamma = Gamma,
                    TimeShiftType = TimeShiftType,
                    UseDynamicShifting = UseDynamicShifting,
                    UseFlowSigmas = UseFlowSigmas,
                    Stages = Stages,                            // TODO
                    StageRange = StageRange,                    // TODO
                },
                SchedulerType.TCD => new TCDOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    Thresholding = Thresholding,
                    DynamicThresholdingRatio = DynamicThresholdingRatio,
                    TimestepScaling = TimestepScaling,
                    ClipSample = ClipSample,
                    ClipSampleRange = ClipSampleRange,
                    SampleMaxValue = SampleMaxValue,
                    OriginalInferenceSteps = OriginalInferenceSteps,
                    SetAlphaToOne = SetAlphaToOne,
                    RescaleBetasZeroSNR = RescaleBetasZeroSNR,
                },
                SchedulerType.SCM => new SCMOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    PredictionType = PredictionType,
                    SigmaData = SigmaData,
                },
                SchedulerType.SASolver => new SASolverOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    BetaEnd = BetaEnd,
                    BetaSchedule = BetaSchedule,
                    BetaStart = BetaStart,
                    PredictionType = PredictionType,
                    TimestepSpacing = TimestepSpacing,
                    StepsOffset = StepsOffset,
                    Thresholding = Thresholding,
                    DynamicThresholdingRatio = DynamicThresholdingRatio,
                    SampleMaxValue = SampleMaxValue,
                    FlowShift = FlowShift,
                    AlgorithmType = AlgorithmType,
                    VarianceType = VarianceType,
                    UseFlowSigmas = UseFlowSigmas,
                    LowerOrderFinal = LowerOrderFinal,
                    PredictorOrder = PredictorOrder,
                    CorrectorOrder = CorrectorOrder,
                    UseBetaSigmas = UseBetaSigmas,
                    UseExponentialSigmas = UseExponentialSigmas,
                    UseKarrasSigmas = UseKarrasSigmas,
                },
                SchedulerType.LTXEulerAncestral => new LTXEulerAncestralRFOptions
                {
                    NumTrainTimesteps = NumTrainTimesteps,
                    Eta = Eta,
                    SNoise = SNoise,
                },
                _ => throw new NotImplementedException(),
            };
        }

        public override string ToString()
        {
            return _scheduler.GetDisplayName();
        }
        public virtual bool Equals(SchedulerInputOptions other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
