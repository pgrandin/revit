using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevit
{
    class Level1 : MyRevit.MyLevel
    {
        public Level1(Document doc)
        {
            this.doc = doc;
            this.levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            this.level_id = 2;
            this.level = levels[level_id];
            this.level_above = levels[3];
        }


        public Result setup_level()
        {
            floorView = setup_view(ViewType.FloorPlan);
            ceilingView = setup_view(ViewType.CeilingPlan);

            WallType wType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>().FirstOrDefault(q
                => q.Name == "2x4 + Gypsum wall with Exterior");

            // Get a floor type for floor creation
            FloorType floorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>().FirstOrDefault(q
                => q.Name == "Wood Joist 10\" - Wood Finish");

            insert_exterior_walls(wType, floorType);

            // stairs opening
            XYZ[][] coords2 =
            {
                    new XYZ[] { new XYZ(180, 141.5, 0), new XYZ(180, 188.5, 0) },
                    new XYZ[] { null,                   new XYZ(314, 188.5, 0) },
                    new XYZ[] { null,                   new XYZ(314, 141.5, 0) },
                    new XYZ[] { null,                   new XYZ(180, 141.5, 0) },
                };

            Transaction trans = new Transaction(doc);
            using (trans = new Transaction(doc))
            {
                trans.Start("Stairs opening");
                CurveArray floor_opening_profile = new CurveArray();    // profile for the floor

                for (int i = 0; i < coords2.Length; i++)
                {
                    Line line;
                    line = Line.CreateBound(
                        (coords2[i][0] is null ?
                            coords2[i - 1][1].Divide(12).Add(new XYZ(0, 0, level.Elevation)) :
                            coords2[i][0].Divide(12).Add(new XYZ(0, 0, level.Elevation))
                        ),

                        coords2[i][1].Divide(12).Add(new XYZ(0, 0, level.Elevation))
                    );
                    floor_opening_profile.Append(line);
                }
                floor.Document.Create.NewOpening(floor, floor_opening_profile, false);
                trans.Commit();
            }

            return Result.Succeeded;
        }


        public Result setup_inside_walls()
        {
            insert_inside_walls();

            // playroom
            add_dimension_from_point(floorView, new XYZ(20, 50, level.Elevation + 1), new XYZ(0, 1, 0), "158\"", new XYZ(-60, 0, 0));
            add_dimension_from_point(floorView, new XYZ(24, 130, level.Elevation + 1), new XYZ(1, 0, 0), "87 1/2\"");
            add_dimension_from_point(floorView, new XYZ(50, 20, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(0, -100, 0));

            // family room
            add_dimension_from_point(floorView, new XYZ(20, 200, level.Elevation + 1), new XYZ(0, 1, 0), "210\"", new XYZ(-60, 0, 0));

            // reading room
            add_dimension_from_point(floorView, new XYZ(362, 20, level.Elevation + 1), new XYZ(1, 0, 0), "213 1/2\"", new XYZ(0, -100, 0));

            // hallway
            add_dimension_from_point(floorView, new XYZ(205, 20, level.Elevation + 1), new XYZ(0, 1, 0));
            add_dimension_from_point(floorView, new XYZ(205, 20, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(0, -100, 0));

            // toilets
            add_dimension_from_point(floorView, new XYZ(140, 20, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(0, -100, 0));

            // dining
            add_dimension_from_point(floorView, new XYZ(400, 270, level.Elevation + 1), new XYZ(1, 0, 0), "132\"");

            // dining + reading
            add_dimension_from_point(floorView, new XYZ(465, 20, level.Elevation + 1), new XYZ(0, 1, 0), "172 1/2\"", new XYZ(60, 0, 0));
            add_dimension_from_point(floorView, new XYZ(465, 250, level.Elevation + 1), new XYZ(0, 1, 0), "124 1/2\"", new XYZ(60, 0, 0));

            // kitchen + living room
            add_dimension_from_point(floorView, new XYZ(170, 330, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(0, 100, 0));
            add_dimension_from_points(floorView, new XYZ(270, 330, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(270, 300, level.Elevation + 1), new XYZ(1, 0, 0), "24\"", new XYZ(0, -50, 0));

            // between columns
            add_dimension_from_point(floorView, new XYZ(400, 185, level.Elevation + 1), new XYZ(1, 0, 0), "107 1/2\"");

            // hallway to kitchen
            add_dimension_from_point(floorView, new XYZ(170, 129, level.Elevation + 1), new XYZ(1, 0, 0));

            // test
            add_dimension_from_points(floorView, new XYZ(20, 20, level.Elevation + 1), new XYZ(400, 20, level.Elevation + 1), new XYZ(-1, 0, 0), "472\"", new XYZ(0, -150, 0));

            add_dimension_from_points(floorView, new XYZ(260, 80, level.Elevation + 1), new XYZ(0, -1, 0), new XYZ(230, 80, level.Elevation + 1), new XYZ(0, -1, 0), "65\"", new XYZ(0, -150, 0));

            return Result.Succeeded;
        }

        public Result setup_joists()
        {
            using (Transaction t = new Transaction(doc, "Joists"))
            {
                t.Start();
                Family f = null;
                //FIXME : move to a function that's called only once
                string familyPath = @"C:\ProgramData\Autodesk\RVT 2019\Libraries\US Imperial\Structural Framing\Wood\Plywood Web Joist.rfa";
                doc.LoadFamily(familyPath, out f);

                XYZ pt0 = XYZ.Zero;
                Line directionLine = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(0, 5, -joist_offset));

                SketchPlane sp = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 40, -joist_offset)));
                BeamSystem bs = BeamSystem.Create(doc, joistCurves, sp, directionLine.Direction, false);

                //get the layoutRule of the beamsystem
                Autodesk.Revit.DB.LayoutRule layoutRule = bs.LayoutRule;

                //create a new instance of the LayoutRuleClearSpacing class
                LayoutRuleClearSpacing myLayoutRuleClearSpacing =
                            new LayoutRuleClearSpacing(2.0, BeamSystemJustifyType.Beginning);

                //set the new layoutRule to the beamsystem
                bs.LayoutRule = myLayoutRuleClearSpacing;

                t.Commit();
            }
            return Result.Succeeded;
        }
        public Result setup()
        {
            setup_level();
            setup_inside_walls();
            // setup_joists();
            return Result.Succeeded;
        }

    }
}
