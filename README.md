# VisualStudioMetricsOnCodeLens
Display Visual Studio's code metrics results on CodeLens.

Save the code and run the analysis.  
The analysis status is output to "VisualStudioMetricsOnCodeLens" in "Output".  
Once the analysis is complete, it will be reflected in CodeLens.

## Require
- Visual Studio 2022

## Options
"options">"Metrics on CodeLens"
- CodeLens description format  
Customize description with placeholders  
%MI%: Maintainability Index  
%CY%: Cyclomatic Complexity  
%CC%: Class Coupling  
%DI%: Depth Of Inheritance  
%SL%: Source Lines  
%EL%: Executable Lines

## Mechanism
1. Hook to save and run analysis
1. Save results in "{slnDir}/.Metrics/{project}.json"
1. Forces a CodeLens update
1. CodeLens will see the results

## Acknowledgements
This software was inspired by the following repositories.
- [tackme31/howmessy](https://github.com/tackme31/howmessy)

## Project contents
Extension require
- source.extension.vsixmanifest
- VisualStudioMetricsOnCodeLensPackage.cs

CodeLens
- Metrics.cs
- MetricsCodeLensDataPoint.cs
- MetricsCodeLensDocumentParser.cs
- MetricsCodeLensProvider.cs

Misc
- SaveCommandHandler.cs
- OptionPage.cs

Utils
- WorkspaceExension.cs
- PipeServerHost.cs
- Logger.cs
