using System;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;

namespace MyRevit
{

    // FailurePreprocessor class required for StairsEditScope
    class StairsFailurePreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            // Use default failure processing
            return FailureProcessingResult.Continue;
        }
    }

    public class walls
    {
        public IList<IList<double>> coords { get; set; }
        public Boolean define_slope { get; set; }
        public double roof_offset { get; set; }
    }


    public class mysets
    {
        public string type { get; set; }
        public string base_level { get; set; }
        public IList<walls> walls { get; set; }
    }

    public class mylevels
    {
        public string level { get; set; }
        public IList<mysets> sets { get; set; }
    }


    class MyLevel
    {
        protected Document doc;
        protected ViewPlan floorView;
        protected ViewPlan ceilingView;
        protected IList<Level> levels = null;
        protected int level_id;
        protected Level level;
        protected Level level_above;
        protected double joist_offset = 2 / 12.0;
        protected List<Curve> joistCurves = new List<Curve>();
        protected Floor floor;
        protected IList<Wall> ext_walls = new List<Wall>();
        protected IList<Wall> int_walls = new List<Wall>();
        protected IList<mysets> dataset = null;

        public MyLevel() { }

        [Flags]
        public enum DoorOperations
        {
            None = 0,
            Should_flip = 1,
            Should_rotate = 2,
        };

        public Result add_dimension_from_point(ViewPlan floorView, XYZ pt, XYZ normal)
        {
            XYZ offset = new XYZ(0, 0, 0);
            return add_dimension_from_point(floorView, pt, normal, "", offset);
        }

        public Result add_dimension_from_point(ViewPlan floorView, XYZ pt, XYZ normal, string dimension)
        {
            XYZ offset = new XYZ(0, 0, 0);
            return add_dimension_from_point(floorView, pt, normal, dimension, offset);
        }

        public Result add_dimension_from_point(ViewPlan floorView, XYZ pt, XYZ normal, XYZ offset)
        {
            return add_dimension_from_point(floorView, pt, normal, "", offset);
        }

        public Result add_dimension_from_point(ViewPlan floorView, XYZ pt, XYZ normal, string dimension, XYZ offset)
        {
            return add_dimension_from_points(floorView, pt, pt, normal, dimension, offset);
        }


        public Result add_dimension_from_points(ViewPlan floorView, XYZ pt1, XYZ pt2, XYZ normal, string dimension, XYZ offset)
        {
            return add_dimension_from_points(floorView, pt1, normal, pt2, -normal, dimension, offset);
        }

        public Result add_dimension_from_points(ViewPlan floorView, XYZ pt1, XYZ normal1, XYZ pt2, XYZ normal2, string dimension, XYZ offset)
        {
            Transaction trans = new Transaction(doc);

            offset = new XYZ(offset.X / 12.0, offset.Y / 12.0, offset.Z);
            pt1 = new XYZ(pt1.X / 12, pt1.Y / 12, pt1.Z);
            pt2 = new XYZ(pt2.X / 12, pt2.Y / 12, pt2.Z);

            ElementClassFilter filter = new ElementClassFilter(typeof(WallType));

            ViewFamilyType viewFamily = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .First(x => x.ViewFamily == ViewFamily.ThreeDimensional);

            View3D view;
            using (trans = new Transaction(doc))
            {
                trans.Start("Create temp view");
                view = View3D.CreateIsometric(doc, viewFamily.Id);
                trans.Commit();
            }

            List<BuiltInCategory> builtInCats
               = new List<BuiltInCategory>();

            builtInCats.Add(BuiltInCategory.OST_Roofs);
            builtInCats.Add(BuiltInCategory.OST_Ceilings);
            builtInCats.Add(BuiltInCategory.OST_Floors);
            builtInCats.Add(BuiltInCategory.OST_Walls);

            ElementMulticategoryFilter intersectFilter = new ElementMulticategoryFilter(builtInCats);

            ReferenceIntersector ri1 = new ReferenceIntersector(intersectFilter, FindReferenceTarget.Face, view);
            ReferenceIntersector ri2 = new ReferenceIntersector(intersectFilter, FindReferenceTarget.Face, view);

            ReferenceWithContext ref1 = ri1.FindNearest(pt1, normal1);
            ReferenceWithContext ref2 = ri2.FindNearest(pt2, normal2);

            using (trans = new Transaction(doc))
            {
                trans.Start("Delete temp view");
                doc.Delete(view.Id);
                trans.Commit();
            }

            ReferenceArray ra = new ReferenceArray();

            ra.Append(ref1.GetReference());
            ra.Append(ref2.GetReference());

            Line line = Line.CreateBound(pt1.Add(offset), pt1.Add(offset).Add(normal1));
            Dimension dim;

            DimensionType dType = null;


            using (Transaction t = new Transaction(doc))
            {
                t.Start("Create New Dimension");
                dim = doc.Create.NewDimension(floorView, line, ra);

                if (dimension.Equals(""))
                {
                    dType = new FilteredElementCollector(doc)
                        .OfClass(typeof(DimensionType))
                        .Cast<DimensionType>().FirstOrDefault(q
                        => q.Name == "type-unknown");
                }
                else if (dimension.Equals(dim.ValueString))
                {
                    dType = new FilteredElementCollector(doc)
                        .OfClass(typeof(DimensionType))
                        .Cast<DimensionType>().FirstOrDefault(q
                        => q.Name == "type-correct");

                }
                else
                {
                    dType = new FilteredElementCollector(doc)
                        .OfClass(typeof(DimensionType))
                        .Cast<DimensionType>().FirstOrDefault(q
                        => q.Name == "type-incorrect");

                }

                dim.DimensionType = dType;
                t.Commit();
            }

            return Result.Succeeded;
        }

        public ViewPlan setup_view(ViewType vt)
        {
            IList<Level> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();

            Transaction trans = new Transaction(doc);
            ViewFamily vf = ViewFamily.Invalid;

            if (vt == ViewType.FloorPlan)
            {
                vf = ViewFamily.StructuralPlan;
            }
            else if (vt == ViewType.CeilingPlan)
            {
                vf = ViewFamily.CeilingPlan;
            }

            ViewFamilyType FviewFamily = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .First(x => x.ViewFamily == vf);

            ViewPlan view = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>().FirstOrDefault(q
                => q.Name == level.Name && q.ViewType == vt);
            // FIXME : need to filter by vf

            using (trans = new Transaction(doc))
            {
                trans.Start("View plans");
                if (view is null) { view = ViewPlan.Create(doc, FviewFamily.Id, level.Id); }
                trans.Commit();
            }

            if (vt == ViewType.FloorPlan)
            {

                FilteredElementCollector col_ = new FilteredElementCollector(doc);
                col_.OfClass(typeof(FamilySymbol));
                col_.OfCategory(BuiltInCategory.OST_TitleBlocks);

                FamilySymbol fs = col_.FirstElement() as FamilySymbol;


                using (trans = new Transaction(doc))
                {
                    trans.Start("Sheet");

                    Family tf = null;
                    //Choose appropriate path
                    string tfamilyPath = @"C:\Users\John\Documents\owncloud\revit\Families\11x8 title block.rfa";
                    doc.LoadFamily(tfamilyPath, out tf);

                    // Get the available title block from document
                    FamilySymbol FS = null;
                    FilteredElementCollector col__ = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_TitleBlocks);
                    Element TB = null;
                    foreach (Element e in col__)
                    {
                        if (e.Name.Contains("11x8"))
                        {
                            TB = e;
                        }
                    }

                    ViewSheet viewSheet = ViewSheet.Create(doc, TB.Id);

                    UV location = new UV((viewSheet.Outline.Max.U - viewSheet.Outline.Min.U) / 2,
                                                         (viewSheet.Outline.Max.V - viewSheet.Outline.Min.V) / 2);

                    //viewSheet.AddView(view3D, location);
                    Viewport.Create(doc, viewSheet.Id, view.Id, new XYZ(location.U, location.V, 0));
                    viewSheet.Name = level.Name;
                    trans.Commit();
                }
            }
            return view;
        }

        public static List<XYZ[]> get_walls_from_file(Level level, string type)
        {
            string jsonFilePath = @"C:\Users\John\AppData\Roaming\Autodesk\Revit\Addins\2019\data.json";

            List<mylevels> items;
            List<XYZ[]> coords = new List<XYZ[]>();

            using (StreamReader r = new StreamReader(jsonFilePath))
            {
                string json = r.ReadToEnd();
                items = JsonConvert.DeserializeObject<List<mylevels>>(json);
            }

            foreach (mylevels l in items)
            {
                if (l.level == level.Name)
                {
                    foreach (mysets s in l.sets)
                    {
                        if (s.type == type)
                        {
                            walls p = null;
                            foreach (walls w in s.walls)
                            {
                                // coords.Add(new XYZ(w.coords[0], w.coords[1], level.Elevation));
                                XYZ a = w.coords[0] is null ?
                                    new XYZ(p.coords[1][0] / 12.0, p.coords[1][1] / 12.0, level.Elevation) :
                                    new XYZ(w.coords[0][0] / 12.0, w.coords[0][1] / 12.0, level.Elevation);
                                XYZ b = new XYZ(w.coords[1][0] / 12.0, w.coords[1][1] / 12.0, level.Elevation);
                                XYZ[] w2 = { a, b };
                                coords.Add(w2);
                                p = w;
                            }
                        }
                    }
                }
            }
            return coords;
        }

        public Result insert_exterior_walls(WallType wType, FloorType floorType)
        {
            Transaction trans = new Transaction(doc);

            List<XYZ[]> coords = get_walls_from_file(level, "exterior");

            using (trans = new Transaction(doc))
            {
                trans.Start("Basement");

                SketchPlane sketch = SketchPlane.Create(doc, level.Id);

                CurveArray floor_profile = new CurveArray();    // profile for the floor

                foreach (XYZ[] p in coords)
                {
                    Line line;

                    line = Line.CreateBound(p[0], p[1]);

                    floor_profile.Append(line);

                    joistCurves.Add(Line.CreateBound(
                        new XYZ(p[0].X, p[0].Y, level_above.Elevation - joist_offset),
                        new XYZ(p[1].X, p[1].Y, level_above.Elevation - joist_offset))
                    );
                    // Workaround for the lack of ceiling
                    doc.Create.NewDetailCurve(ceilingView, line);

                    // Create structural, concrete walls
                    Wall wall = Wall.Create(doc, line as Curve, level.Id, true);
                    wall.WallType = wType;

                    ext_walls.Add(wall);

                    Parameter p_ = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                    if (null != p_)
                    {
                        p_.Set(level_above.Id);
                    }
                    p_ = wall.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    if (null != p_)
                    {
                        p_.Set(level.Name + "_ext_" + ext_walls.Count().ToString());
                    }

                }

                if (floorType != null)
                {
                    XYZ normal = XYZ.BasisZ;
                    floor = doc.Create.NewFloor(floor_profile, floorType, level, true, normal);
                }
                trans.Commit();
            }
            return Result.Succeeded;
        }

        public Result insertfloor(FloorType floorType)
        {
            Transaction trans = new Transaction(doc);

            List<XYZ[]> coords = get_walls_from_file(level, "floor");

            using (trans = new Transaction(doc))
            {
                trans.Start("floor");
                CurveArray floor_profile = new CurveArray();
                foreach (XYZ[] p in coords)
                {
                    Line line;

                    line = Line.CreateBound(p[0], p[1]);

                    floor_profile.Append(line);

                }

                XYZ normal = XYZ.BasisZ;
                floor = doc.Create.NewFloor(floor_profile, floorType, level, true, normal);
                trans.Commit();
            }
            return Result.Succeeded;
        }

        public Result insert_inside_walls()
        {
            Transaction trans = new Transaction(doc);

            List<XYZ[]> coords = get_walls_from_file(level, "interior");

            WallType wType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>().FirstOrDefault(q
                => q.Name == "2x4 + Gypsum");


            using (trans = new Transaction(doc))
            {
                trans.Start("Inside walls");
                SketchPlane sketch = SketchPlane.Create(doc, level.Id);

                foreach (XYZ[] p in coords)
                {
                    Line line;
                    line = Line.CreateBound(p[0], p[1]);

                    // Create non structural walls
                    Wall wall = Wall.Create(doc, line as Curve, level.Id, false);
                    wall.WallType = wType;

                    Parameter p_ = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                    if (null != p_)
                    {
                        p_.Set(level_above.Id);
                    }
                    p_ = wall.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    if (null != p_)
                    {
                        p_.Set(level.Name + "_int_" + int_walls.Count().ToString());
                    }

                    int_walls.Add(wall);
                }
                trans.Commit();
            }
            return Result.Succeeded;
        }

        public Result insert_doors(XYZ[] doors_locations, Level level)
        {
            FamilySymbol fs = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>().FirstOrDefault(q
                => q.Name == "36\" x 84\"");

            using (Transaction t = new Transaction(doc))
            {
                t.Start("Activate door");

                if (!fs.IsActive)
                { fs.Activate(); doc.Regenerate(); }
                t.Commit();
            }


            foreach (XYZ location in doors_locations)
            {
                DoorOperations orientation = (DoorOperations)location.Z;
                XYZ door_location = new XYZ(location.X, location.Y, level.Elevation);

                #region Find the hosting Wall (nearest wall to the insertion point)

                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.OfClass(typeof(Wall));

                List<Wall> walls = collector.Cast<Wall>().Where(wl => wl.LevelId == level.Id).ToList();

                Wall w_ = null;

                double distance = double.MaxValue;

                foreach (Wall w in walls)
                {
                    double proximity = (w.Location as LocationCurve).Curve.Distance(door_location);

                    if (proximity < distance)
                    {
                        distance = proximity;
                        w_ = w;
                    }
                }

                #endregion

                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Create door");

                    FamilyInstance door = doc.Create.NewFamilyInstance(door_location, fs, w_, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    // Doors probably face up by default. Or maybe it depends on the wall direction?
                    // E-W wall = door N by default

                    if ((orientation & DoorOperations.Should_flip) != 0)
                    {
                        door.flipFacing();
                    }
                    if ((orientation & DoorOperations.Should_rotate) != 0)
                    {
                        door.flipHand();
                    }
                    t.Commit();
                }
            }
            return Result.Succeeded;
        }

        public Result insert_windows(XYZ[] windows_locations, Level level)
        {
            FamilySymbol wfs = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>().FirstOrDefault(q
                => q.Name == "48\" x 60\"");


            using (Transaction t = new Transaction(doc))
            {
                t.Start("Activate window");

                if (!wfs.IsActive)
                { wfs.Activate(); doc.Regenerate(); }
                t.Commit();
            }

            foreach (XYZ location in windows_locations)
            {
                DoorOperations orientation = (DoorOperations)location.Z;
                XYZ window_location = new XYZ(location.X, location.Y, level.Elevation + 2.5); // Windows should be at 30" from the ground

                #region Find the hosting Wall (nearest wall to the insertion point)

                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.OfClass(typeof(Wall));

                List<Wall> walls = collector.Cast<Wall>().Where(wl => wl.LevelId == level.Id).ToList();

                Wall w_ = null;

                double distance = double.MaxValue;

                foreach (Wall w in walls)
                {
                    double proximity = (w.Location as LocationCurve).Curve.Distance(window_location);

                    if (proximity < distance)
                    {
                        distance = proximity;
                        w_ = w;
                    }
                }

                #endregion

                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Create window");

                    FamilyInstance window = doc.Create.NewFamilyInstance(window_location, wfs, w_, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                    if ((orientation & DoorOperations.Should_flip) != 0)
                    {
                        window.flipFacing();
                    }
                    if ((orientation & DoorOperations.Should_rotate) != 0)
                    {
                        window.flipHand();
                    }
                    t.Commit();
                }
            }

            return Result.Succeeded;
        }

    }
}
