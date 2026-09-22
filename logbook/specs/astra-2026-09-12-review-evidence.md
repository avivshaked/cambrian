# Evidence for the Astra review of 12 September 2026

These probes support the review. The tests used the source at a73e535. During review, commit 55d14ac added research notes without changing that source. No Unity worker was used.

## The source fingerprints identify what was tested

- `src/Evosim.Core/Environment/GridField.cs`: `9dc92222ce5021230f05559a0f08fdcade0b6fe88ecd7d2b7d5ee90ca5210709`

- `unity/Assets/Evosim/Sim/SharedVolume.cs`: `5bee46e038a7e2a1a693ee8ccca63a511cabb2264e161308c91542267b134f6f`

- `unity/Assets/Evosim/Sim/Ecosystem.cs`: `70fb880ac5e9398fe4c4effb1de6872ab7bbe2a3e0629cb7b4bf7de8d9c11883`

- `scripts/positions-read.py`: `ffac9e477c7a822ec90fb8556b426bf8947d694230440459cf1475e2f582b2f5`

- `scripts/compare-det.py`: `2460bc3b73ee76ea985e46bf60b2551ef87b74074bbac4d110fa29d6697e2fc5`


## The uniform-field probe calls the current Core implementation

The field starts at 1 unit per cubic metre. There are no organisms, deposits, settling, refuge, or burial. Mixing is either off or at the campaign rate: 0.02 for metre cells and 2 for five-metre cells. The current uses the campaign seed stream, speed 0.1, period 6,000, and a half-second step. CV is the standard deviation divided by the mean.

Put the following files under the repository paths named above each block. Run the Core build first, then the probe with the Unity-bundled .NET SDK. The project references the built Core assembly.


`scratch/astra-20260912/Probe.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup><Reference Include="Evosim.Core"><HintPath>../../artifacts/Evosim.Core/bin/Debug/netstandard2.1/Evosim.Core.dll</HintPath></Reference><Compile Remove="placement/**/*.cs" /></ItemGroup>
</Project>
```


`scratch/astra-20260912/Program.cs`

```csharp
using Evosim.Core;

foreach (WorldShape shape in new[] {WorldShape.Box, WorldShape.Tank})
foreach (float cell in new[] {1f, 5f})
foreach (bool mix in new[] {false, true}) {
  var radius = shape == WorldShape.Tank ? TankGeometry.RadiusFor(100f) : 0f;
  var field = new GridField(100f, 0f, 60f, 0f, 0f, 4, cell, 1, shape, radius);
  var current = new CurrentField {Mode=CurrentMode.Transport, Speed=0.1f, PeriodSeconds=6000f, AdvectFields=true};
  current.SetBox(5f, 4, 60f, Rng.SeedFor(1UL, World.CurrentFieldIndex), 1, shape, radius);
  field.SeedUniform(1f);
  double initial = field.Recount();
  for (int step = 1; step <= 1200; step++) {
    if (mix) field.Mix(.5f, cell == 1f ? .02f : 2f);
    field.Advect(current, step*.5, .5f, field.PatchWidthMetres);
    if (step != 1 && step != 200 && step != 1200) continue;
    double min=double.MaxValue, max=0, sum=0, square=0; int n=0;
    for (int x=0;x<field.CellsX;x++) for (int y=0;y<field.CellsY;y++) for (int z=0;z<field.CellsZ;z++) {
      if (!field.IsLive(x,y,z)) continue;
      double d=field.JoulesAt(x,y,z)/(cell*cell*cell);
      min=Math.Min(min,d); max=Math.Max(max,d); sum+=d; square+=d*d; n++;
    }
    double mean=sum/n, cv=Math.Sqrt(Math.Max(0,square/n-mean*mean))/mean;
    Console.WriteLine($"{shape} cell={cell} mix={mix} t={step*.5} min={min:F6} max={max:F6} CV={cv:P3} totalError={(field.Recount()-initial)/initial:E3}");
  }
}
```


The output uses the machine locale, where a comma is the decimal separator.

```text
Box cell=1 mix=False t=0,5 min=0,946276 max=1,059153 CV=1,794 % totalError=9,095E-016
Box cell=1 mix=False t=100 min=0,031978 max=5,820185 CV=57,262 % totalError=6,063E-016
Box cell=1 mix=False t=600 min=0,083173 max=3,952715 CV=55,604 % totalError=3,486E-015
Box cell=1 mix=True t=0,5 min=0,946276 max=1,059153 CV=1,794 % totalError=9,095E-016
Box cell=1 mix=True t=100 min=0,387978 max=2,498119 CV=30,381 % totalError=-7,579E-016
Box cell=1 mix=True t=600 min=0,357010 max=2,454204 CV=30,357 % totalError=-6,063E-016
Box cell=5 mix=False t=0,5 min=0,985713 max=1,014125 CV=0,690 % totalError=1,516E-016
Box cell=5 mix=False t=100 min=0,197283 max=3,446016 CV=75,904 % totalError=0,000E+000
Box cell=5 mix=False t=600 min=0,001770 max=4,646454 CV=89,812 % totalError=-3,032E-016
Box cell=5 mix=True t=0,5 min=0,985713 max=1,014125 CV=0,690 % totalError=1,516E-016
Box cell=5 mix=True t=100 min=0,906068 max=1,104100 CV=4,920 % totalError=0,000E+000
Box cell=5 mix=True t=600 min=0,906593 max=1,112720 CV=5,234 % totalError=-3,032E-016
Tank cell=1 mix=False t=0,5 min=0,932545 max=1,048132 CV=1,001 % totalError=6,063E-016
Tank cell=1 mix=False t=100 min=0,000005 max=5,587650 CV=50,943 % totalError=4,547E-016
Tank cell=1 mix=False t=600 min=0,000000 max=13,816296 CV=75,230 % totalError=7,579E-016
Tank cell=1 mix=True t=0,5 min=0,932545 max=1,048132 CV=1,001 % totalError=6,063E-016
Tank cell=1 mix=True t=100 min=0,054575 max=2,792800 CV=32,443 % totalError=-1,061E-015
Tank cell=1 mix=True t=600 min=0,209786 max=3,064227 CV=29,157 % totalError=-1,213E-015
Tank cell=5 mix=False t=0,5 min=0,994634 max=1,006363 CV=0,332 % totalError=-1,516E-016
Tank cell=5 mix=False t=100 min=0,396922 max=1,790051 CV=32,641 % totalError=1,516E-016
Tank cell=5 mix=False t=600 min=0,173886 max=4,391022 CV=90,088 % totalError=-1,516E-016
Tank cell=5 mix=True t=0,5 min=0,994634 max=1,006363 CV=0,332 % totalError=-1,516E-016
Tank cell=5 mix=True t=100 min=0,947701 max=1,048996 CV=2,895 % totalError=-1,516E-016
Tank cell=5 mix=True t=600 min=0,889419 max=1,145755 CV=7,144 % totalError=0,000E+000
```


## The placement probe tests geometry without running physics

This compiles the unchanged placement class against a small arithmetic shim. The shim supplies vector and trigonometric operations; it implements no collision solver. The direct call asks whether a half-metre bounding sphere fits one centimetre inside a wall slab. The second check draws independent unit-cube founders in an otherwise empty tank. It counts cubes with a corner outside the mathematical circle. Solver behavior was not measured.


`scratch/astra-20260912/placement/Placement.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup>
    <Reference Include="Evosim.Core"><HintPath>../../../artifacts/Evosim.Core/bin/Debug/netstandard2.1/Evosim.Core.dll</HintPath></Reference>
    <Compile Include="../../../unity/Assets/Evosim/Sim/SharedVolume.cs" Link="SharedVolume.cs" />
  </ItemGroup>
</Project>
```


`scratch/astra-20260912/placement/Shim.cs`

```csharp
// Arithmetic-only shim for compiling the unchanged placement class outside Unity.
// No collider, engine step, or PhysX behavior is simulated here.
namespace UnityEngine {
 public struct Vector3 {
   public float x,y,z;
   public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
   public float sqrMagnitude => x*x+y*y+z*z;
   public static Vector3 operator -(Vector3 a,Vector3 b) => new(a.x-b.x,a.y-b.y,a.z-b.z);
   public static float Distance(Vector3 a,Vector3 b) => MathF.Sqrt((a-b).sqrMagnitude);
 }
 public static class Mathf {
   public const float PI = MathF.PI;
   public static float Max(float a,float b)=>MathF.Max(a,b);
   public static int Max(int a,int b)=>Math.Max(a,b);
   public static int Clamp(int v,int a,int b)=>Math.Clamp(v,a,b);
   public static float Sqrt(float v)=>MathF.Sqrt(v);
   public static float Sin(float v)=>MathF.Sin(v);
   public static float Cos(float v)=>MathF.Cos(v);
   public static float Floor(float v)=>MathF.Floor(v);
   public static float Round(float v)=>MathF.Round(v);
   public static int FloorToInt(float v)=>(int)MathF.Floor(v);
   public static int CeilToInt(float v)=>(int)MathF.Ceiling(v);
 }
}
namespace Evosim.Sim {
 public class SeaFloor { public float MinimumPlacementY(float radius)=>-60+radius; }
}
```


`scratch/astra-20260912/placement/Program.cs`

```csharp
using Evosim.Core;
using Evosim.Sim;
using UnityEngine;
using System.Reflection;
var r=TankGeometry.RadiusFor(100f);
var volume=new SharedVolume(4,5,60,1,5,1,WorldShape.Tank,r);
var free=typeof(SharedVolume).GetMethod("Free",BindingFlags.Instance|BindingFlags.NonPublic)!;
float angle=MathF.PI/48; // tangent midpoint of an actual wall slab
var at=new Vector3(r+(r-.01f)*MathF.Cos(angle),-10,r+(r-.01f)*MathF.Sin(angle));
Console.WriteLine($"Tank R={r:R}; centre radius R-0.01; body radius=0.5; Free={free.Invoke(volume,new object[]{at,.5f})}; radial overhang=0.49m");
var genome=new Genome();
genome.Nodes.Add(new MorphNode{Dimensions=new Float3(.5f,.5f,.5f), JointType=JointType.Fixed});
var phenotype=Developer.Develop(genome);
int overlap=0; int samples=1000;
for(int i=0;i<samples;i++) {
 volume.Begin(); float height=-10;
 if(!volume.TryReserveFounder(phenotype,ref height,out _)) throw new Exception("refused");
 volume.Commit(i); volume.TryTakePlacement(i,out var p);
 // A cube at identity is what the harness builds: test its eight corners in the footprint.
 bool crossed=false;
 foreach(float dx in new[]{-.5f,.5f}) foreach(float dz in new[]{-.5f,.5f})
  if(!TankGeometry.Inside(p.x+dx,p.z+dz,r)) crossed=true;
 if(crossed) overlap++;
}
Console.WriteLine($"Unit cube founders with at least one corner outside circle: {overlap}/{samples}; no physics run.");
```


```text
Tank R=5,641896; centre radius R-0.01; body radius=0.5; Free=True; radial overhang=0.49m
Unit cube founders with at least one corner outside circle: 208/1000; no physics run.
```


## Existing suites and script reproductions bound the checks

The default Core suite passed 653 tests in 78 seconds. Its 13 slow experiments were excluded. The clade scorer passed 39 assertions across six fixtures. The feeding-log reader passed eight checks. Script fixtures were generated in a scratch copy to preserve the repository fixtures.

The positions suite passed its positive cases, then stopped at line 179 under Windows PowerShell 5.1. Its expected missing-arm stderr became a terminating NativeCommandError before the exit-code assertion. This run therefore did not pass the whole suite.

Two one-row stats files, with the same time and different alive counts, made the current comparison script print a difference and exit zero. Two differing digest hashes without body dumps made the digest script exit one. A passing scorer fixture with an ended/wall manifest at 10,000 of 30,000 seconds printed CENSORED.

The smaller-passes fixture was also given this manifest:

```json
{"status":"ended","reason":"budget","requestedSeconds":10000,"simulatedSeconds":10000}
```

The scorer printed a plain PASS for clade root 2000, with 17 alive and a stability minimum of 14. No duration qualification or censoring label was appended.

The live round-37 seed-5 report at sample 3,200 contained 628 alive and 110/100 occupied columns. Its sample 3,300 contained 601 alive and 111/100 occupied columns. The round was still running when these instrument readings were checked.

## These commands reproduce the Core and geometry checks

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/core-test.ps1
$reviewDotnet = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe"
& $reviewDotnet run --project scratch/astra-20260912/Probe.csproj
& $reviewDotnet run --project scratch/astra-20260912/placement/Placement.csproj
```
