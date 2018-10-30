#r "paket: groupref FakeBuild //"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.IO
open Fake.BuildServer
open Fake.IO.Globbing.Operators
open Fake.DotNet
open Fake.DotNet.NuGet
open Fake.Core
open Fake.Tools

BuildServer.install [
    AppVeyor.Installer
]

let isAppveyor = AppVeyor.detect()
let gitVersion = GitVersion.generateProperties id

Target.create "Clean" (fun _ ->
  ["reports" ; "src/common" ; "nuget/tools"]
  |> Seq.iter Directory.delete

  !! "nuget/*"
  -- "nuget/*.txt"
  -- "nuget/*.nuspec"
  |> File.deleteAll

  let configuration = 
    (fun p -> { p with 
                  Properties = ["Configuration", "Release"]
                  Verbosity = Some MSBuildVerbosity.Minimal })

  !! "src/BCC.Submission.sln"
  |> MSBuild.run configuration null "Clean" list.Empty
  |> Trace.logItems "Clean-Output: "
)

Target.create "Build" (fun _ ->
  CreateProcess.fromRawWindowsCommandLine "gitversion" "/updateassemblyinfo src\common\SharedAssemblyInfo.cs /ensureassemblyinfo"
  |> Proc.run
  |> ignore

  let configuration = (fun p -> { p with 
                                    DoRestore = true
                                    Verbosity = Some MSBuildVerbosity.Minimal })

  !! "src/BCC.Submission.sln"
  |> MSBuild.runRelease configuration null "Build"
  |> Trace.logItems "AppBuild-Output: "
)

Target.create "Test" (fun _ ->
    List.allPairs ["BCC.Submission.Tests"] ["net471" ; "netcoreapp2.1"]
    |> Seq.iter (fun (proj, framework) -> 
            let projectPath = sprintf "src\\%s\\%s.csproj" proj proj
            let reportFile = sprintf "%s-%s.results.trx" proj framework

            let configuration: (DotNet.TestOptions -> DotNet.TestOptions)
                = (fun t -> {t with
                               Configuration = DotNet.BuildConfiguration.Release
                               NoBuild = true
                               Framework = Some framework
                               Logger = Some (sprintf "trx;LogFileName=%s" reportFile)
                               ResultsDirectory = Some "../../reports"})

            DotNet.test configuration projectPath
            
            Trace.publish ImportData.BuildArtifact (sprintf "reports/%s" reportFile)
    )
)

Target.create "Package" (fun _ ->
    Shell.copyRecursive "src/BCC.Submission/bin/Release" "nuget/tools" false
    |> ignore

    let version = 
        match String.isNullOrWhiteSpace gitVersion.PreReleaseLabel with
        | false -> sprintf "%s-%s%s" gitVersion.MajorMinorPatch gitVersion.PreReleaseLabel gitVersion.BuildMetaDataPadded
        | _ -> sprintf "%s" gitVersion.MajorMinorPatch

    NuGet.NuGetPack (fun p -> { p with
                                  Version = version
                                  OutputPath = "nuget" }) "nuget/Package.nuspec"

    !! "nuget/*.nupkg"
    |> Seq.iter (Trace.publish ImportData.BuildArtifact)
)

Target.create "Coverage" (fun _ ->
    List.allPairs ["BCC.Submission.Tests"] ["net471" ; "netcoreapp2.1"]
    |> Seq.iter (fun (proj, framework) -> 
            let dllPath = sprintf "src\\%s\\bin\\Release\\%s\\%s.dll" proj framework proj
            let projectPath = sprintf "src\\%s\\%s.csproj" proj proj
            let reportPath = sprintf "reports/%s-%s.coverage.xml" proj framework

            sprintf "%s --target \"dotnet\" --targetargs \"test -c Release -f %s %s --no-build\" --format opencover --output \"./%s\""
                dllPath framework projectPath reportPath
            |> CreateProcess.fromRawWindowsCommandLine "coverlet"
            |> Proc.run
            |> ignore

            Trace.publish ImportData.BuildArtifact reportPath

            if isAppveyor then
                CreateProcess.fromRawWindowsCommandLine "codecov" (sprintf "-f \"%s\"" reportPath)
                |> Proc.run
                |> ignore
        )
)

Target.create "Default" (fun _ -> ())

open Fake.Core.TargetOperators
"Clean" ==> "Build"

"Build" ==> "Package" ==> "Default"
"Build" ==> "Test" ==> "Default"
"Build" ==> "Coverage" ==> "Default"

// start build
Target.runOrDefault "Default"
