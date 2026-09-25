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
