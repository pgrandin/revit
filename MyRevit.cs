
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevit
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]


    public class Class1 : IExternalCommand
    {

        public void setup_elevation_markers(Document doc)
        {
            Transaction trans = new Transaction(doc);
            FilteredElementCollector levels_col
              = new FilteredElementCollector(doc)
                .OfClass(typeof(ElevationMarker));

            while (levels_col.Count() > 0)
            {
                ElevationMarker m = levels_col.Last() as ElevationMarker;
                using (trans = new Transaction(doc))
                {
                    trans.Start("elevation markers");
                    doc.Delete(m.Id);
                    trans.Commit();
                }
            }

            ViewFamilyType vft = new FilteredElementCollector(doc)
                   .OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                    .FirstOrDefault<ViewFamilyType>(a => ViewFamily.Elevation == a.ViewFamily);

            XYZ center = new XYZ(60, 10, 0);
            // FIXME : select the Floor plan/Site view
            View view = doc.ActiveView;
            using (trans = new Transaction(doc))
            {
                trans.Start("elevation views");
                ElevationMarker marker = ElevationMarker.CreateElevationMarker(doc, vft.Id, center, 50);
                ViewSection elevationView = marker.CreateElevation(doc, view.Id, 0);
                Parameter p = elevationView.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);
                p.Set(300.0);
                trans.Commit();
            }

        }

        public Result setup_levels(Document doc)
        {
            Transaction trans = new Transaction(doc);
            FilteredElementCollector levels_col
              = new FilteredElementCollector(doc)
                .OfClass(typeof(Level));

            /*
            Level[] levels = new Level[5];
            foreach (Level lvl_ in levels_col)
            {
                if (lvl_.Name == "Level 1")
                {
                    levels[1] = lvl_;
                }
                if (lvl_.Name == "Level 2")
                {
                    levels[2] = lvl_;
                }
                if (lvl_.Name == "Basement")
                {
                    levels[0] = lvl_;
                }
            }
            */

            // FIXME : delete Floor plans/Level 1


            using (Transaction t = new Transaction(doc))
            {
                t.Start("Activate door");
                doc.Regenerate();
                t.Commit();
            }

            FilteredElementCollector floorplans
              = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan));



            List<ViewPlan> ViewPlans = floorplans.Cast<ViewPlan>().ToList();

            using (trans = new Transaction(doc))
            {
                trans.Start("Clean up views");

                foreach (ViewPlan view in ViewPlans)
                {
                    if (view.Name == "Level 2")
                    {
                        doc.Delete(view.Id);
                    }
                }

                trans.Commit();
            }


            using (trans = new Transaction(doc))
            {
                trans.Start("Basement");
                Level lvl = Level.Create(doc, -8.5);
                lvl.Name = "Basement";
                trans.Commit();
            }
            
            using (trans = new Transaction(doc))
            {
                trans.Start("Roof");
                Level lvl = Level.Create(doc, 20);
                lvl.Name = "Roof";
                trans.Commit();
            }
            
            using (trans = new Transaction(doc))
            {
                trans.Start("Garage");
                Level lvl = Level.Create(doc, -2);
                lvl.Name = "Garage";
                trans.Commit();
            }


            FilteredElementCollector DimensionTypeCollector = new FilteredElementCollector(doc);
            DimensionTypeCollector.OfClass(typeof(DimensionType));

            DimensionType dimensionType = DimensionTypeCollector.Cast<DimensionType>().ToList().FirstOrDefault();
            using (trans = new Transaction(doc))
            {
                trans.Start("SetDimensionsTypes");
                DimensionType newdimensionType = dimensionType.Duplicate("type-correct") as DimensionType;
                newdimensionType.LookupParameter("Color").Set(0);

                newdimensionType = dimensionType.Duplicate("type-unknown") as DimensionType;
                newdimensionType.LookupParameter("Color").Set(125);

                newdimensionType = dimensionType.Duplicate("type-incorrect") as DimensionType;
                newdimensionType.LookupParameter("Color").Set(255);

                trans.Commit();
            }
            return Result.Succeeded;
        }

        public Result setup_wall_struct(Document doc)
        {

            WallType wallType = null;

            // FIXME : replace with the material approach?
            FilteredElementCollector wallTypes
              = new FilteredElementCollector(doc)
              .OfClass(typeof(WallType));

            foreach (WallType wt in wallTypes)
            {
                if (wt.Name.Equals("Generic - 8\""))
                {
                    wallType = wt;
                    break;
                }
            }

            Material concrete = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>().FirstOrDefault(q
                => q.Name == "Concrete, Cast-in-Place gray");

            Material insulation = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>().FirstOrDefault(q
                => q.Name == "EIFS, Exterior Insulation");

            Material lumber = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>().FirstOrDefault(q
                => q.Name == "Softwood, Lumber");

            Material gypsum = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>().FirstOrDefault(q
                => q.Name == "Gypsum Wall Board");

            WallType newWallType = null;

            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("Basement exterior wall");

                newWallType = wallType.Duplicate("Basement") as WallType;
                // FIXME : this layer should have the "structural material" parameter set
                CompoundStructureLayer l1 = new CompoundStructureLayer(8.0 / 12, MaterialFunctionAssignment.Structure, concrete.Id);
                CompoundStructureLayer newLayer = new CompoundStructureLayer(3.0 / 12, MaterialFunctionAssignment.Insulation, insulation.Id);
                CompoundStructureLayer newLayer2 = new CompoundStructureLayer(3.5 / 12, MaterialFunctionAssignment.Finish1, lumber.Id);
                CompoundStructureLayer newLayer3 = new CompoundStructureLayer(.5 / 12, MaterialFunctionAssignment.Finish2, gypsum.Id);

                CompoundStructure structure = newWallType.GetCompoundStructure();

                IList<CompoundStructureLayer> layers = structure.GetLayers();

                layers.Add(l1);
                layers.Add(newLayer);
                layers.Add(newLayer2);
                layers.Add(newLayer3);
                structure.SetLayers(layers);

                structure.DeleteLayer(0);

                structure.SetNumberOfShellLayers(ShellLayerType.Interior, 3);

                newWallType.SetCompoundStructure(structure);

                tx.Commit();
            }

            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("2x4 + Gypsum wall");

                newWallType = wallType.Duplicate("2x4 + Gypsum") as WallType;
                CompoundStructureLayer l1 = new CompoundStructureLayer(3.5 / 12, MaterialFunctionAssignment.Structure, lumber.Id);
                CompoundStructureLayer l2 = new CompoundStructureLayer(0.5 / 12, MaterialFunctionAssignment.Finish1, gypsum.Id);

                CompoundStructure structure = newWallType.GetCompoundStructure();

                IList<CompoundStructureLayer> layers = structure.GetLayers();

                layers.Add(l2);
                layers.Add(l1);
                layers.Add(l2);
                structure.SetLayers(layers);

                structure.DeleteLayer(0);
                structure.SetNumberOfShellLayers(ShellLayerType.Exterior, 1);
                structure.SetNumberOfShellLayers(ShellLayerType.Interior, 1);

                newWallType.SetCompoundStructure(structure);

                tx.Commit();
            }

            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("2x4 + Gypsum wall with Exterior");

                newWallType = wallType.Duplicate("2x4 + Gypsum wall with Exterior") as WallType;
                CompoundStructureLayer l1 = new CompoundStructureLayer(3.5 / 12, MaterialFunctionAssignment.Structure, lumber.Id);
                CompoundStructureLayer l2 = new CompoundStructureLayer(0.5 / 12, MaterialFunctionAssignment.Finish1, gypsum.Id);

                CompoundStructure structure = newWallType.GetCompoundStructure();

                IList<CompoundStructureLayer> layers = structure.GetLayers();

                layers.Add(l2);
                layers.Add(l1);
                layers.Add(l2);
                structure.SetLayers(layers);

                structure.DeleteLayer(0);
                structure.SetNumberOfShellLayers(ShellLayerType.Exterior, 1);
                structure.SetNumberOfShellLayers(ShellLayerType.Interior, 1);


                ElementType wallSweepType = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Cornices)
                    .WhereElementIsElementType()
                    .Cast<ElementType>().FirstOrDefault();

                if (wallSweepType != null)
                {
                    var wallSweepInfo = new WallSweepInfo(WallSweepType.Sweep, false);
                    wallSweepInfo.Distance = 2;

                    List<WallSweepInfo> ModSW = new List<WallSweepInfo>();
                    // structure.AddWallSweep(wallSweepInfo);
                }


                newWallType.SetCompoundStructure(structure);

                tx.Commit();
            }

            return Result.Succeeded;
        }

        public Result setup_units(Document doc)
        {
            Units units_doc = doc.GetUnits();

            UnitType ut = UnitType.UT_Length;
            FormatOptions ft_doc = units_doc.GetFormatOptions(ut);
            FormatOptions nFt = new FormatOptions();
            nFt.UseDefault = false;
            nFt.DisplayUnits = DisplayUnitType.DUT_FRACTIONAL_INCHES;
            units_doc.SetFormatOptions(ut, nFt);

            Transaction trans;
            using (trans = new Transaction(doc))
            {
                trans.Start("Units");
                doc.SetUnits(units_doc);
                trans.Commit();
            }

            return Result.Succeeded;
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Get application and document objects
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            ViewPlan site = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>().FirstOrDefault(q
                => q.Name == "Site");

            uiapp.ActiveUIDocument.RequestViewChange(site);


            setup_units(doc);
            setup_elevation_markers(doc);
            setup_levels(doc);
            setup_wall_struct(doc);

            var b = new Basement(doc);
            b.setup();
            var l1 = new Level1(doc);
            l1.setup();
            var l2 = new Level2(doc);
            l2.setup();
            var g = new Garage(doc);
            g.setup();
            var r = new Roof(doc);
            r.setup_roof();

            // uiapp.ActiveUIDocument.RequestViewChange(floorView);

            return Result.Succeeded;
        }
    }
}
