using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bonsai;
using Bonsai.Design;
using Bonsai.Dsp.Design;
using OpenCV.Net;
using PSTH;

[assembly: TypeVisualizer(typeof(HistogramListVisualizer), Target = typeof(HistogramList))]

namespace PSTH
{
    public class HistogramListVisualizer : DialogTypeVisualizer
    {
        private const int TargetElapsedTime = (int)(1000.0 / 30);
        private bool _requireInvalidate;
        private Timer _updateTimer;
        private HistogramListView _graph;

        public override void Load(IServiceProvider provider)
        {
            _graph = new HistogramListView();
            _graph.Dock = DockStyle.Fill;
            var visualizerService = (IDialogTypeVisualizerService)provider.GetService(typeof(IDialogTypeVisualizerService));
            if (visualizerService == null) return;
            visualizerService.AddControl(_graph);
            visualizerService.AddControl(_graph);
            _updateTimer = new Timer() {Interval = TargetElapsedTime};
            _updateTimer.Tick += (sender, e) =>
            {
                if (!_requireInvalidate) return;
                _graph.InvalidateTimeSeries();
                _requireInvalidate = false;
            };
            _updateTimer.Start();
        }

        public override void Unload()
        {
            _updateTimer.Stop();
            _updateTimer.Dispose();
            _graph.Dispose();
            _updateTimer = null;
            _graph = null;
        }

        public override void Show(object value)
        {
            var histogramList = (HistogramList)value;
            if (histogramList == null || histogramList.Count == 0) return;
            _graph.UpdateTimeSeries(histogramList);
            _requireInvalidate = true;
        }
    }
}
