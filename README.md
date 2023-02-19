## PSTH: Bonsai Plugin for online spike histogram from Open Ephys data

## To run debug

- Install Bonsai.
- Add the .nupkg file to the Bonsai Gallery (usually located in C:\Users\%Username%\AppData\Local\Bonsai\Gallery).
- Install the pacakge from the Bonsai "Manage Packages". Choose "Gallery" from "Package source" and check "Include prerelease".
- Install the latest version of NetMQ from NuGet.
- Install OpenEphys with Network Events and ZMQ Interface. Replace the ZMQ Interface dll with the updated one from [here](https://github.com/Jialiang-Lu/zmq-interface/releases/).
- Launch OpenEphys and set up the workflow.
- Open the TestTTL.bonsai and start.
- Start the experiment.

### What's done

- OpenEphysParser
    - Converts `NetMQMessage` to `Timestamped<OpenEphysData>`.
    - Properly timestamp the samples based on SampleNumber from OpenEphys instead of local clock.
- FilterOpenEphysData
    - Can filter the outputs based on type, sortedId and eventType.
- WindowBackTrigger
    - Similar to SampleWindow, but added a buffer to store samples from the past.
    - Kept as a standalone module, but not needed for histograms.
- SpikeHistogram
    - Combines `Timestamped<OpenEphysData>` with triggers `Timestamped<TClass>` to generate a sequence of `HistogramList`
    - The `HistogramList` contains a list of 2D arrays that represents histograms of each sorted unit. The X axis is time and Y axis categories of `TClass`. Values are in Hz. `BinEdges` are the edges of the histogram in ms. The `HistogramList` also contains the list of units and classes for plotting. 

- Test.bonsai
    - Example workflow of calculating PSTH triggered on key press (Numpad 1-3). Space can be used to clear the histograms.
- TestTTL.bonsai
    - Example workflow of calculating PSTH triggered on parsed PSTH commands according to [this schema](https://open-ephys.atlassian.net/wiki/spaces/OEW/pages/23265293/PSTH)

### To-do list

- Discard trials based on outcomes
- Triggered LFP
