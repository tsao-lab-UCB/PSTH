## PSTH: Bonsai Plugin for online spike histogram from Open Ephys data

## To run debug

- Install Bonsai with relavant packages, including Bonsai.Dsp, Bonsai.Windows.Input, Bonsai.ZeroMQ, OpenCV.Net, Newtonsoft.Json.
- Install OpenEphys with ZMQInterface. Run OpenEphys on the same computer and add the ZMQInterface to the default FileReader workflow.
- Open the solution and run in debug mode.
- Open the Test.bonsai.

### What's done

- OpenEphysParser
    - Converts `NetMQMessage` to `Timestamped<OpenEphysData>`
    - Can filter the outputs based on type and sortedId
    - Creating timestamp from sampleNumber still produces offset. Why?
- FilterOpenEphysData
    - Probably unnecessary
- WindowBackTrigger
    - Kept as a standalone module, but not needed for histograms
- SpikeHistogram
    - Combines `Timestamped<OpenEphysData>` with triggers `Timestamped<TClass>` and reset signal to generate a sequence of `HistogramCollection<TClass>`
    - The `HistogramCollection<TClass>` contains a sequence of `OpenCV.Net.Mat` that represents histograms of each sorted unit. The X axis is time and Y axis categories of `TClass`. Values are in Hz. `BinEdges` are the edges of the histogram in ms. The `HistogramCollection<TClass>` also contains the list of units and classes for plotting. 

- Test.bonsai
    - Example workflow of calculating PSTH triggered on key press (Numpad 1-3). Space can be used to clear the histograms.

### To-do list

- Histogram filtering using `CV.Filter2D`.
- Better visualizers for the `HistogramCollection<TClass>` with proper x and y labels in a gridded layout.
- Adding another `Publisher` to take information from Kofiko for plotting the legend.
