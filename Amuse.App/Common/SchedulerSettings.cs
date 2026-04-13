using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Python.Scheduler;

namespace Amuse.App.Common
{
    public record SchedulerSettings
    {
        public SchedulerSettings() { }
        protected SchedulerSettings(SchedulerSettings other)
        {
            LMS = CopyScheduler(other.LMS);
            Euler = CopyScheduler(other.Euler);
            EulerAncestral = CopyScheduler(other.EulerAncestral);
            DDPM = CopyScheduler(other.DDPM);
            DDIM = CopyScheduler(other.DDIM);
            KDPM2 = CopyScheduler(other.KDPM2);
            KDPM2Ancestral = CopyScheduler(other.KDPM2Ancestral);
            DDPMWuerstchen = CopyScheduler(other.DDPMWuerstchen);
            LCM = CopyScheduler(other.LCM);
            DPMSolverMultistep = CopyScheduler(other.DPMSolverMultistep);
            DPMSolverSinglestep = CopyScheduler(other.DPMSolverSinglestep);
            DPMSolverSDE = CopyScheduler(other.DPMSolverSDE);
            DEISMultistep = CopyScheduler(other.DEISMultistep);
            EDMEuler = CopyScheduler(other.EDMEuler);
            EDMDPMSolverMultistep = CopyScheduler(other.EDMDPMSolverMultistep);
            FlowMatchEuler = CopyScheduler(other.FlowMatchEuler);
            FlowMatchHeun = CopyScheduler(other.FlowMatchHeun);
            FlowMatchLCM = CopyScheduler(other.FlowMatchLCM);
            PNDM = CopyScheduler(other.PNDM);
            Heun = CopyScheduler(other.Heun);
            UniPCMultistep = CopyScheduler(other.UniPCMultistep);
            IPNDM = CopyScheduler(other.IPNDM);
            CogVideoXDDIM = CopyScheduler(other.CogVideoXDDIM);
            CogVideoXDPM = CopyScheduler(other.CogVideoXDPM);
            Helios = CopyScheduler(other.Helios);
            HeliosDMD = CopyScheduler(other.HeliosDMD);
            TCD = CopyScheduler(other.TCD);
            SCM = CopyScheduler(other.SCM);
            SASolver = CopyScheduler(other.SASolver);
            LTXEulerAncestralRF = CopyScheduler(other.LTXEulerAncestralRF);
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LMSOptions LMS { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EulerOptions Euler { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EulerAncestralOptions EulerAncestral { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DDPMOptions DDPM { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DDIMOptions DDIM { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public KDPM2Options KDPM2 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public KDPM2AncestralOptions KDPM2Ancestral { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DDPMWuerstchenOptions DDPMWuerstchen { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LCMOptions LCM { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DPMSolverMultistepOptions DPMSolverMultistep { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DPMSolverSinglestepOptions DPMSolverSinglestep { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DPMSolverSDEOptions DPMSolverSDE { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DEISMultistepOptions DEISMultistep { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EDMEulerOptions EDMEuler { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EDMDPMSolverMultistepOptions EDMDPMSolverMultistep { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FlowMatchEulerOptions FlowMatchEuler { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FlowMatchHeunOptions FlowMatchHeun { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FlowMatchLCMOptions FlowMatchLCM { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PNDMOptions PNDM { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public HeunOptions Heun { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public UniPCMultistepOptions UniPCMultistep { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IPNDMOptions IPNDM { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CogVideoXDDIMOptions CogVideoXDDIM { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CogVideoXDPMOptions CogVideoXDPM { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public HeliosOptions Helios { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public HeliosDMDOptions HeliosDMD { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TCDOptions TCD { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SCMOptions SCM { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SASolverOptions SASolver { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LTXEulerAncestralRFOptions LTXEulerAncestralRF { get; set; }


        public SchedulerOptions GetScheduler(SchedulerType scheduler)
        {
            return GetSchedulers().FirstOrDefault(x => x.Scheduler == scheduler);
        }


        public IEnumerable<SchedulerOptions> GetSchedulers()
        {
            if (LMS != null) yield return LMS;
            if (Euler != null) yield return Euler;
            if (EulerAncestral != null) yield return EulerAncestral;
            if (DDPM != null) yield return DDPM;
            if (DDIM != null) yield return DDIM;
            if (KDPM2 != null) yield return KDPM2;
            if (KDPM2Ancestral != null) yield return KDPM2Ancestral;
            if (DDPMWuerstchen != null) yield return DDPMWuerstchen;
            if (LCM != null) yield return LCM;
            if (DPMSolverMultistep != null) yield return DPMSolverMultistep;
            if (DPMSolverSinglestep != null) yield return DPMSolverSinglestep;
            if (DPMSolverSDE != null) yield return DPMSolverSDE;
            if (DEISMultistep != null) yield return DEISMultistep;
            if (EDMEuler != null) yield return EDMEuler;
            if (EDMDPMSolverMultistep != null) yield return EDMDPMSolverMultistep;
            if (FlowMatchEuler != null) yield return FlowMatchEuler;
            if (FlowMatchHeun != null) yield return FlowMatchHeun;
            if (FlowMatchLCM != null) yield return FlowMatchLCM;
            if (PNDM != null) yield return PNDM;
            if (Heun != null) yield return Heun;
            if (UniPCMultistep != null) yield return UniPCMultistep;
            if (IPNDM != null) yield return IPNDM;
            if (CogVideoXDDIM != null) yield return CogVideoXDDIM;
            if (CogVideoXDPM != null) yield return CogVideoXDPM;
            if (Helios != null) yield return Helios;
            if (HeliosDMD != null) yield return HeliosDMD;
            if (TCD != null) yield return TCD;
            if (SCM != null) yield return SCM;
            if (SASolver != null) yield return SASolver;
            if (LTXEulerAncestralRF != null) yield return LTXEulerAncestralRF;
        }


        private static T CopyScheduler<T>(T source) where T : SchedulerOptions
        {
            return source is not null ? source with { } : null;
        }

    }
}
