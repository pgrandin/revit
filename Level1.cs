using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevit
{
    class Level1 : MyRevit.MyLevel
    {
        public Level1(UIApplication uiapp, IList<Paint> paints)
        {
            this.uiapp = uiapp;
            this.doc = uiapp.ActiveUIDocument.Document;
            this.levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            this.level_id = 2;
            this.level = levels[level_id];
            this.level_above = levels[3];
            this.paints = paints;

            setup_level();
            setup_inside_walls();
            // setup_joists();
            setup_doors();
            CreateRoomAndSeperators();
            create_wall_openings();
            paint_walls();
            
            ExportToImage(floorView);
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
            add_dimension_from_point(floorView, new XYZ(30, 50, level.Elevation + 1), new XYZ(0, 1, 0), "158\"", new XYZ(-90, 0, 0));
            add_dimension_from_point(floorView, new XYZ(24, 130, level.Elevation + 1), new XYZ(1, 0, 0), "87 1/2\"");
            add_dimension_from_point(floorView, new XYZ(50, 20, level.Elevation + 1), new XYZ(1, 0, 0), "123 1/2\"", new XYZ(0, -100, 0));

            // playroom to stairs
            add_dimension_from_points(floorView, new XYZ(175, 150, level.Elevation + 1), new XYZ(0, 1, 0), new XYZ(195, 90, level.Elevation + 1), new XYZ(0, 1, 0), "81\"", new XYZ(-15, 0, 0));
            // { "coords": [null, [300, 106.5]]},
            add_dimension_from_points(floorView, new XYZ(175, 109, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(310, 109, level.Elevation + 1), new XYZ(-1, 0, 0), "116\"", new XYZ(0, -15, 0));

            add_dimension_from_point(floorView, new XYZ(250, 90, level.Elevation + 1), new XYZ(0, 1, 0), "40\"", new XYZ(90, 0, 0));

            // family room
            add_dimension_from_point(floorView, new XYZ(30, 200, level.Elevation + 1), new XYZ(0, 1, 0), "210\"", new XYZ(-90, 0, 0));
            add_dimension_from_point(floorView, new XYZ(8, 200, level.Elevation + 1), new XYZ(0, 1, 0), "59\"", new XYZ(-30, 0, 0));
            add_dimension_from_point(floorView, new XYZ(8, 360, level.Elevation + 1), new XYZ(0, 1, 0), "34\"", new XYZ(-30, 0, 0));

            add_dimension_from_points(floorView, new XYZ(8, 200, level.Elevation + 1), new XYZ(0, 1, 0), new XYZ(8, 360, level.Elevation + 1), new XYZ(0, -1, 0), "117\"", new XYZ(-30, 0, 0));

            add_dimension_from_point(floorView, new XYZ(24, 170, level.Elevation + 1), new XYZ(1, 0, 0), "86\"");
            add_dimension_from_points(floorView, new XYZ(85, 190, level.Elevation + 1), new XYZ(0, -1, 0), new XYZ(100, 190, level.Elevation + 1), new XYZ(0, -1, 0), "24\"", new XYZ(0, 0, 0));

            // reading room
            add_dimension_from_point(floorView, new XYZ(362, 20, level.Elevation + 1), new XYZ(1, 0, 0), "213 1/2\"", new XYZ(0, -100, 0));

            // mudroom
            add_dimension_from_point(floorView, new XYZ(225, 20, level.Elevation + 1), new XYZ(0, 1, 0), "60\"");
            add_dimension_from_point(floorView, new XYZ(205, 20, level.Elevation + 1), new XYZ(1, 0, 0), "38\"", new XYZ(0, -100, 0));
            add_dimension_from_point(floorView, new XYZ(240, 20, level.Elevation + 1), new XYZ(1, 0, 0), "23 1/2\"", new XYZ(0, -100, 0));

            // right side AC pillar
            add_dimension_from_points(floorView, new XYZ(460, 195, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(460, 175, level.Elevation + 1), new XYZ(1, 0, 0), "12\"", new XYZ(0, 15, 0));
            add_dimension_from_points(floorView, new XYZ(465, 195, level.Elevation + 1), new XYZ(0, -1, 0), new XYZ(465, 170, level.Elevation + 1), new XYZ(0, 1, 0), "19\"", new XYZ(60, 0, 0));
            // left side AC pillar
            add_dimension_from_points(floorView, new XYZ(360, 195, level.Elevation + 1), new XYZ(-1, 0, 0), new XYZ(360, 175, level.Elevation + 1), new XYZ(-1, 0, 0), "12\"", new XYZ(0, 15, 0));

            // powder room
            add_dimension_from_point(floorView, new XYZ(140, 20, level.Elevation + 1), new XYZ(1, 0, 0), "55\"", new XYZ(0, -100, 0));

            // dining
            add_dimension_from_point(floorView, new XYZ(400, 270, level.Elevation + 1), new XYZ(1, 0, 0), "132\"");

            // dining + reading
            add_dimension_from_point(floorView, new XYZ(465, 20, level.Elevation + 1), new XYZ(0, 1, 0), "172 1/2\"", new XYZ(60, 0, 0));
            add_dimension_from_point(floorView, new XYZ(465, 250, level.Elevation + 1), new XYZ(0, 1, 0), "124 1/2\"", new XYZ(60, 0, 0));

            // kitchen
            add_dimension_from_point(floorView, new XYZ(315, 250, level.Elevation + 1), new XYZ(0, 1, 0), "184\"", new XYZ(0, 0, 0));
            add_dimension_from_point(floorView, new XYZ(174, 250, level.Elevation + 1), new XYZ(0, 1, 0), "153\"", new XYZ(0, 0, 0));

            // kitchen + living room
            add_dimension_from_point(floorView, new XYZ(170, 360, level.Elevation + 1), new XYZ(1, 0, 0), "334\"", new XYZ(0, 100, 0));
            add_dimension_from_point(floorView, new XYZ(150, 200, level.Elevation + 1), new XYZ(1, 0, 0), "169 1/2\"", new XYZ(0, 10, 0));
            add_dimension_from_point(floorView, new XYZ(200, 200, level.Elevation + 1), new XYZ(1, 0, 0), "160\"", new XYZ(0, 10, 0));

            // between columns
            add_dimension_from_point(floorView, new XYZ(400, 185, level.Elevation + 1), new XYZ(1, 0, 0), "107 1/2\"");

            // hallway to kitchen
            add_dimension_from_point(floorView, new XYZ(170, 129, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(0, 100, 0));

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

        public Result setup_doors()
        {
            XYZ[] doors_locations = {
                new XYZ(187.5 / 12.0, 43 / 12.0, 0.0),  // powder room
                new XYZ(285 / 12.0, 0.0, (double) DoorOperations.Should_rotate), // entrance
                new XYZ(209 / 12.0, 0.0, (double) DoorOperations.Should_rotate), // garage
            };

            return insert_doors(doors_locations, level);
        }

        public void CreateRoomAndSeperators()
        {
            /*
            { "coords": [[476, 191.5], [463, 191.5]]},
            { "coords": [null, [463, 177]]},
            { "coords": [null, [476, 177]]},
            { "coords": [[340, 191.5], [350.5, 191.5]]},
            { "coords": [null, [350.5, 177]]},
            { "coords": [null, [331, 177]]}
            */

            XYZ pt1 = new XYZ(463 / 12.0, 184.25 / 12.0, 0);
            XYZ pt2 = new XYZ(350.5 / 12.0, 184.25 / 12.0, 0);

            UV pt5 = new UV(5, 5);

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(pt1, pt2));

            using (Transaction trans = new Transaction(doc))
            {
                trans.Start("Create Room and Boundaries");
                ModelCurveArray lines = doc.Create.NewRoomBoundaryLines(floorView.SketchPlane, curveArray, floorView);
                // Room room = doc.Create.NewRoom(activeView.GenLevel, pt5);
                //now modify the "room" Element's Name, Number, etc.
                trans.Commit();
            }
        }

        public void create_wall_openings(){
            using (Transaction trans = new Transaction(doc))
            {

                trans.Start("Create Room and Boundaries");

                // [338.5, 320], [338.5, 191]
                // Kitchen opening is 35x93", along the X axis
                XYZ start_point = new XYZ(330 / 12.0, 318 / 12.0, 0);
                XYZ opening = new XYZ(1, 35 / 12.0, 93 / 12.0);
                doc.Create.NewOpening(
                    int_walls[0],
                    start_point,
                    new XYZ(start_point.X + opening.X, start_point.Y - opening.Y, start_point.Z + opening.Z)
                );
                trans.Commit();
            }

        }

        public void paint_walls(){

            paint_wall(ext_walls[0], ShellLayerType.Interior, "SW6840");

            int[] sw7009_int = { 9 };
            foreach (int i in sw7009_int){
                paint_wall(int_walls[i], ShellLayerType.Interior, "SW7009");
            }

            int[] sw7009_ext = {0, 1, 2, 21, 22, 23, 24, 25, 26};
            foreach (int i in sw7009_ext)            {
                paint_wall(int_walls[i], ShellLayerType.Exterior, "SW7009");
            }

            int[] sw7050_int = {7, 10, 12};
            foreach (int i in sw7050_int){
                paint_wall(int_walls[i], ShellLayerType.Interior, "SW7050");
            }

            int[] sw7050_ext = {3, 4, 5, 6, 12, 13, 14, 15, 16, 17, 18, 19, 20};
            foreach (int i in sw7050_ext){
                paint_wall(int_walls[i], ShellLayerType.Exterior, "SW7050");
            }

            int[] sw7009_int2 = { 8, 10, 11, 12, 13 };
            foreach (int i in sw7009_int2){
                paint_wall(ext_walls[i], ShellLayerType.Interior, "SW7009");
            }

            int[] sw7050_int2 = { 2, 3, 4, 5, 6 };
            foreach (int i in sw7050_int2){
                paint_wall(ext_walls[i], ShellLayerType.Interior, "SW7050");
            }

        }

    }
}
