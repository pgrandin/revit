using System;
using System.IO;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

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
        protected UIApplication uiapp;
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
        protected IList<Paint> paints = null;

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
                vf = ViewFamily.FloorPlan;
                // vf = ViewFamily.StructuralPlan;
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
                    // string tfamilyPath = @"C:\Users\John\Documents\owncloud\revit\Families\11x8 title block.rfa";
                    string tfamilyPath = @"C:\ProgramData\Autodesk\RVT 2023\Libraries\English-Imperial\Titleblocks\A 8.5 x 11 Vertical.rfa";
                    doc.LoadFamily(tfamilyPath, out tf);

                    // Get the available title block from document
                    FamilySymbol FS = null;
                    FilteredElementCollector col__ = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_TitleBlocks);
                    Element TB = null;
                    foreach (Element e in col__)
                    {
                        // if (e.Name.Contains("11x8"))
                        if (e.Name.Contains("A 8.5 x 11 Vertical"))
                        {
                            FS = e as FamilySymbol;
                            TB = e;
                            break;
                        }
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
            string jsonFilePath = @"C:\Users\Admin\AppData\Roaming\Autodesk\Revit\Addins\2023\data.json";

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
                trans.Start("Exterior walls");

                SketchPlane sketch = SketchPlane.Create(doc, level.Id);
                CurveLoop floor_loop = new CurveLoop();
                
                trans.Commit();

                foreach (XYZ[] p in coords)
                {
                    Line line;
                    trans.Start("Exterior walls");
                    line = Line.CreateBound(p[0], p[1]);

                    floor_loop.Append(line);

                    joistCurves.Add(Line.CreateBound(
                        new XYZ(p[0].X, p[0].Y, level_above.Elevation - joist_offset),
                        new XYZ(p[1].X, p[1].Y, level_above.Elevation - joist_offset))
                    );
                    // Workaround for the lack of ceiling
                    doc.Create.NewDetailCurve(ceilingView, line);

                    // Create structural, concrete walls
                    Wall wall = Wall.Create(doc, line as Curve, level.Id, true);
                    wall.WallType = wType;

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
                    trans.Commit();

                    // Currently wall sweeps only attach to the exterior side
                    // add_wall_sweep(wall);

                    ext_walls.Add(wall);

                }

                if (floorType != null)
                {
                    trans.Start("Exterior walls");
                    XYZ normal = XYZ.BasisZ;

                    // create a new floor using the profile
                    floor = Floor.Create(doc, new List<CurveLoop> {floor_loop}, floorType.Id, level.Id);                  
                    
                   
                    trans.Commit();
                }
            }
            return Result.Succeeded;
        }


        public Result add_wall_sweep(Wall wall)
        {
            Transaction trans = new Transaction(doc);
            ElementType wallSweepType = new FilteredElementCollector(doc)
                  .OfCategory(BuiltInCategory.OST_Cornices)
                  .WhereElementIsElementType()
                  .Cast<ElementType>().FirstOrDefault();
            if (wallSweepType != null)
            {
                var wallSweepInfo = new WallSweepInfo(WallSweepType.Sweep, false);
                wallSweepInfo.Distance = 0;
                trans.Start("External walls sweep");
                WallSweep.Create(wall, wallSweepType.Id, wallSweepInfo);
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
                CurveLoop floor_loop = new CurveLoop();
                
                foreach (XYZ[] p in coords)
                {
                    Line line;

                    line = Line.CreateBound(p[0], p[1]);

                    floor_loop.Append(line);

                }

                XYZ normal = XYZ.BasisZ;
                floor = Floor.Create(doc, new List<CurveLoop> { floor_loop }, floorType.Id, level.Id);
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

            WallType wType_6 = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>().FirstOrDefault(q
                => q.Name == "2x6 + Gypsum");

            using (trans = new Transaction(doc))
            {
                trans.Start("Inside walls");
                SketchPlane sketch = SketchPlane.Create(doc, level.Id);
                trans.Commit();

                foreach (XYZ[] p in coords)
                {
                    trans.Start("Inside walls");
                    Line line;
                    line = Line.CreateBound(p[0], p[1]);

                    // Create non structural walls
                    Wall wall = Wall.Create(doc, line as Curve, level.Id, false);

                    // Temporary solution for the one 2x6 wall
                    if (level.Name == "Level 1" && int_walls.Count() == 1)
                    {
                        wall.WallType = wType_6;
                    }
                    else
                    {
                        wall.WallType = wType;
                    }

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
                    trans.Commit();

                    add_wall_sweep(wall);

                    int_walls.Add(wall);
                }
            }
            return Result.Succeeded;
        }

        public Wall find_hosting_wall(XYZ location){
            #region Find the hosting Wall (nearest wall to the insertion point)

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(Wall));

            List<Wall> walls = collector.Cast<Wall>().Where(wl => wl.LevelId == level.Id).ToList();

            Wall w_ = null;

            double distance = double.MaxValue;

            foreach (Wall w in walls)
            {
                double proximity = (w.Location as LocationCurve).Curve.Distance(location);

                if (proximity < distance)
                {
                    distance = proximity;
                    w_ = w;
                }
            }

            #endregion

            return w_;
        }

        public Result insert_doors(XYZ[] doors_locations, Level level)
        {
            FamilySymbol fs = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>().FirstOrDefault(q
                => q.Name == "30\" x 80\"");

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

                Wall w = find_hosting_wall(door_location);

                if (w != null)
                {
                    using (Transaction t = new Transaction(doc))
                    {
                        t.Start("Create door");

                        FamilyInstance door = doc.Create.NewFamilyInstance(door_location, fs, w, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
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
            }
            return Result.Succeeded;
        }

        public Result insert_windows(XYZ[] windows_locations, Level level)
        {
            FamilySymbol wfs = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>().FirstOrDefault(q
                => q.Name == "SW 0.6x1.2");


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

                Wall w = find_hosting_wall(window_location);

                if (w != null)
                {
                    using (Transaction t = new Transaction(doc))
                    {
                        t.Start("Create window");

                        FamilyInstance window = doc.Create.NewFamilyInstance(window_location, wfs, w, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

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
                } else {
                    TaskDialog.Show("Error", "Could not find a wall to host the window");
                }
            }

            return Result.Succeeded;
        }

        public Result ExportToImage(View view)
        {
            Result r = Result.Failed;

            using (Transaction tx = new Transaction(this.doc))
            {
                tx.Start("Export Image");

                string desktop_path = Environment.GetFolderPath(
                  Environment.SpecialFolder.Desktop);
               
                string filepath = Path.Combine(desktop_path, view.Name);

                ImageExportOptions img = new ImageExportOptions();

                img.ZoomType = ZoomFitType.FitToPage;
                img.PixelSize = 1920;
                img.ImageResolution = ImageResolution.DPI_600;
                img.FitDirection = FitDirectionType.Horizontal;
                img.ExportRange = ExportRange.CurrentView;
                img.HLRandWFViewsFileType = ImageFileType.PNG;
                img.FilePath = filepath;
                img.ShadowViewsFileType = ImageFileType.PNG;

                this.doc.ExportImage(img);

                tx.RollBack();

                filepath = Path.ChangeExtension(filepath, "png");

                r = Result.Succeeded;
            }
            return r;
        }

        public void paint_wall(Wall wall, ShellLayerType slt, string color){
            LocationCurve locationCurve = wall.Location as LocationCurve;
            Curve curve = locationCurve.Curve;
            // Get the side faces
            IList<Reference> sideFaces = HostObjectUtils.GetSideFaces(wall, slt);
            // access the side face
            Face face = doc.GetElement(sideFaces[0]).GetGeometryObjectFromReference(sideFaces[0]) as Face;            

            // Find the paint with name "SW7009" from the IList of paints
            Paint paint = paints.Where(p => p.Name == color).First();
            // paint the wall
            using (Transaction trans = new Transaction(doc))
            {
                trans.Start("Paint wall face");
                doc.Paint(wall.Id, face, paint.Material.Id);
                trans.Commit();
            }
        }

    }
}
