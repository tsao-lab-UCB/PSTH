using Bonsai;
using NetMQ;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace PSTH
{
    [Combinator]
    [Description("")]
    [WorkflowElementCategory(ElementCategory.Transform)]
    public class TrialEventParser
    {
        [Description("Filter trials based on trial outcome.")]
        public bool FilterOutcome { get; set; }

        public IObservable<Timestamped<string>> Process(IObservable<NetMQMessage> source, 
            IObservable<Timestamped<OpenEphysData>> events)
        {
            throw new NotImplementedException();
        }
    }
}
