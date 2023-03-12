using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevit
{
    class Level2 : MyRevit.MyLevel
    {
        public Level2(UIApplication uiapp, IList<Paint> paints)
        {
            this.uiapp = uiapp;
            this.doc = uiapp.ActiveUIDocument.Document;
            this.levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            this.level_id = 3;
            this.level = levels[level_id];
            this.level_above = levels[4];
            this.paints = paints;

            setup_level();
            setup_floor();
            setup_inside_walls();
            setup_doors();
            setup_windows();
            paint_walls();

            create_section_view();

            ExportToImage(floorView);
        }

        public Result create_section_view_for_wall(Wall wall, bool reverse_direction = false)
        {
            ViewFamilyType vft
                 = new FilteredElementCollector(doc)
                   .OfClass(typeof(ViewFamilyType))
                   .Cast<ViewFamilyType>()
                   .FirstOrDefault<ViewFamilyType>(x =>
                     ViewFamily.Section == x.ViewFamily);

            // Determine section box

            LocationCurve lc = wall.Location as LocationCurve;

            Line line = lc.Curve as Line;

            if (reverse_direction)
            {
                line = Line.CreateBound(line.GetEndPoint(1), line.GetEndPoint(0));
            }
            XYZ p = line.GetEndPoint(0);
            XYZ q = line.GetEndPoint(1);
            XYZ v = q - p;

            BoundingBoxXYZ bb = wall.get_BoundingBox(null);
            double minZ = bb.Min.Z;
            double maxZ = bb.Max.Z;

            double w = v.GetLength();
            double d = wall.WallType.Width;
            double offset = 0.1 * w;

            double wallBaseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();
            double wallUnconnectedHeight = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();

            XYZ min = new XYZ(-w, wallBaseOffset - offset, -offset * 0.5);
            XYZ max = new XYZ(w, wallBaseOffset + wallUnconnectedHeight + offset, offset);


            XYZ midpoint = p + 0.5 * v;
            
            XYZ walldir = v.Normalize();
            XYZ up = XYZ.BasisZ;
            XYZ viewdir = walldir.CrossProduct(up);

            Transform t = Transform.Identity;
            t.Origin = midpoint;
            t.BasisX = walldir;
            t.BasisY = up;
            t.BasisZ = viewdir;

            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
            sectionBox.Transform = t;
            sectionBox.Min = min;
            sectionBox.Max = max;

            // Create wall section view

            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("Create Wall Section View");

                ViewSection.CreateSection(doc, vft.Id, sectionBox);

                tx.Commit();
            }
            return Result.Succeeded;
        }

        public Result create_section_view()
        {
            create_section_view_for_wall(ext_walls[0], true);

            return Result.Succeeded;

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

            insert_exterior_walls(wType, null);

            return Result.Succeeded;
        }

        public Result setup_floor()
        {
            // Get a floor type for floor creation
            FloorType floorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>().FirstOrDefault(q
                => q.Name == "Wood Joist 10\" - Wood Finish");

            insertfloor(floorType);
            return Result.Succeeded;
        }

        public Result setup_inside_walls()
        {
            insert_inside_walls();

            // Pierre's Office
            add_dimension_from_point(floorView, new XYZ(425, 290, level.Elevation + 1), new XYZ(1, 0, 0), "120\"");
            add_dimension_from_point(floorView, new XYZ(460, 290, level.Elevation + 1), new XYZ(0, 1, 0), "132\"", new XYZ(50, 0, 0));
            add_dimension_from_point(floorView, new XYZ(460, 150, level.Elevation + 1), new XYZ(0, 1, 0), "43\"", new XYZ(50, 0, 0));

            // Bedroom
            add_dimension_from_point(floorView, new XYZ(425, 30, level.Elevation + 1), new XYZ(1, 0, 0), "131 1/2\"");
            add_dimension_from_point(floorView, new XYZ(460, 40, level.Elevation + 1), new XYZ(0, 1, 0), "132\"", new XYZ(50, 0, 0));
            add_dimension_from_point(floorView, new XYZ(380, 145, level.Elevation + 1), new XYZ(1, 0, 0), "59 1/2\"");

            // Hallway
            add_dimension_from_point(floorView, new XYZ(330, 214, level.Elevation + 1), new XYZ(1, 0, 0), "36\"");
            add_dimension_from_point(floorView, new XYZ(200, 20, level.Elevation + 1), new XYZ(1, 0, 0), "175 1/2\"");

            // master Bedroom
            add_dimension_from_point(floorView, new XYZ(150, -50, level.Elevation + 1), new XYZ(1, 0, 0), "244 1/2\"", new XYZ(0, -210, 0));
            add_dimension_from_point(floorView, new XYZ(150, -150, level.Elevation + 1), new XYZ(0, 1, 0), "222 1/2\"");
            add_dimension_from_point(floorView, new XYZ(220, -150, level.Elevation + 1), new XYZ(0, 1, 0), "183\"", new XYZ(50, 0, 0));
            add_dimension_from_points(floorView, new XYZ(80, -50, level.Elevation + 1), new XYZ(-1, 0, 0), new XYZ(80, -60, level.Elevation + 1), new XYZ(-1, 0, 0), "68 1/2\"", new XYZ(30, 0, 0));
            add_dimension_from_points(floorView, new XYZ(69, -5, level.Elevation + 1), new XYZ(0, -1, 0), new XYZ(75, -5, level.Elevation + 1), new XYZ(0, -1, 0), "167\"", new XYZ(30, 0, 0));
            add_dimension_from_points(floorView, new XYZ(210, -25, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(210, -60, level.Elevation + 1), new XYZ(1, 0, 0), "29\"", new XYZ(30, 0, 0));
            add_dimension_from_points(floorView, new XYZ(20, -40, level.Elevation + 1), new XYZ(0, 1, 0), new XYZ(20, -100, level.Elevation + 1), new XYZ(0, -1, 0), "203 1/2\"", new XYZ(-50, 0, 0));

            // Elodie's Office
            add_dimension_from_point(floorView, new XYZ(245, 305, level.Elevation + 1), new XYZ(1, 0, 0), "131\"");
            add_dimension_from_point(floorView, new XYZ(200, 305, level.Elevation + 1), new XYZ(0, 1, 0), "122\"", new XYZ(-50, 0, 0));

            // bathroom
            add_dimension_from_point(floorView, new XYZ(10, 60, level.Elevation + 1), new XYZ(0, 1, 0), "115 1/2\"", new XYZ(-40, 0, 0));
            add_dimension_from_point(floorView, new XYZ(10, 140, level.Elevation + 1), new XYZ(0, 1, 0), "65 1/2\"", new XYZ(-40, 0, 0));
            add_dimension_from_point(floorView, new XYZ(60, 20, level.Elevation + 1), new XYZ(1, 0, 0), "113 1/2\"");
            add_dimension_from_point(floorView, new XYZ(50, 160, level.Elevation + 1), new XYZ(1, 0, 0), "100\"", new XYZ(0, 50, 0));

            // laundry room
            add_dimension_from_point(floorView, new XYZ(125, 20, level.Elevation + 1), new XYZ(1, 0, 0), "36\"");
            add_dimension_from_point(floorView, new XYZ(125, 20, level.Elevation + 1), new XYZ(0, 1, 0), "65 1/2\"", new XYZ(50, 0, 0));

            // bathroom 2
            add_dimension_from_point(floorView, new XYZ(200, 200, level.Elevation + 1), new XYZ(0, 1, 0), "59\"", new XYZ(-50, 0, 0));

            // floor cut
            add_dimension_from_point(floorView, new XYZ(300, 10, level.Elevation - 0.5), new XYZ(0, 1, 0), "62\"");
            add_dimension_from_point(floorView, new XYZ(300, 10, level.Elevation - 0.5), new XYZ(1, 0, 0), "78\"", new XYZ(0, -40, 0));

            return Result.Succeeded;
        }

        public Result setup_doors()
        {
            XYZ[] doors_locations = {
                new XYZ(439 / 12.0, 184 / 12.0, (double) (DoorOperations.Should_flip | DoorOperations.Should_rotate)),  // Office, closet
                new XYZ(397 / 12.0, 136 / 12.0, 0.0),  // bedroom, closet
                new XYZ(352 / 12.0, 111 / 12.0, (double) (DoorOperations.Should_rotate)),  // bedroom, door
                new XYZ(352 / 12.0, 220 / 12.0, 0.0),  // Office, door
                new XYZ(311.5 / 12.0, 230 / 12.0, 0.0),  // Bathroom, door
                new XYZ(311.5 / 12.0, 268 / 12.0, (double) (DoorOperations.Should_rotate)),  // Office 2, door
                new XYZ(81 / 12.0, 120 / 12.0, 0.0),  // Powder room, door
                new XYZ(42 / 12.0, -60 / 12.0, (double) (DoorOperations.Should_rotate)),  // Walk in closet
            };

            return insert_doors(doors_locations, level);
        }

        public Result setup_windows()
        {
            XYZ[] windows_locations = {
                new XYZ(258 / 12.0, 377 / 12.0, (double) DoorOperations.Should_flip),  // Family 'Sliding_Window_6261', Type 'SW 0.6x1.2'
                new XYZ(418 / 12.0, 323 / 12.0, (double) DoorOperations.Should_flip),  // Family 'Sliding_Window_6261', Type 'SW 0.6x1.2'
            };

            return insert_windows(windows_locations, level);
        }

        public void paint_walls(){

            // Revit walls exterior is left hand side, interior is right hand side
            int[] sw7009_int = {5, 6, 7, 8, 9, 12, 13, 22 };
            foreach (int i in sw7009_int){
                paint_wall(int_walls[i], ShellLayerType.Interior, "SW7009");
            }

            int[] sw7009_ext = {2, 3, 4, 22};
            foreach (int i in sw7009_ext)            {
                paint_wall(int_walls[i], ShellLayerType.Exterior, "SW7009");
            }

            int[] sw7050_int = {};
            foreach (int i in sw7050_int){
                paint_wall(int_walls[i], ShellLayerType.Interior, "SW7050");
            }

            int[] sw7050_ext = {
                12, 14, 16, //office 1
                10
            };
            foreach (int i in sw7050_ext){
                paint_wall(int_walls[i], ShellLayerType.Exterior, "SW7050");
            }

            int[] sw7009_int2 = {8};
            foreach (int i in sw7009_int2){
                paint_wall(ext_walls[i], ShellLayerType.Interior, "SW7009");
            }

            int[] sw7050_int2 = {
                0, // office 1
            };
            foreach (int i in sw7050_int2){
                paint_wall(ext_walls[i], ShellLayerType.Interior, "SW7050");
            }

        }

    }
}
