using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevit
{
    class Level2 : MyRevit.MyLevel
    {
        public Level2(Document doc)
        {
            this.doc = doc;
            this.levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            this.level_id = 3;
            this.level = levels[level_id];
            this.level_above = levels[4];
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
            add_dimension_from_point(floorView, new XYZ(10, 140, level.Elevation + 1), new XYZ(0, 1, 0), "48\"", new XYZ(-40, 0, 0));
            add_dimension_from_point(floorView, new XYZ(60, 20, level.Elevation + 1), new XYZ(1, 0, 0), "113 1/2\"");
            add_dimension_from_point(floorView, new XYZ(50, 160, level.Elevation + 1), new XYZ(1, 0, 0), "100\"", new XYZ(0, 50, 0));

            // bathroom 2
            add_dimension_from_point(floorView, new XYZ(200, 200, level.Elevation + 1), new XYZ(0, 1, 0), "59\"", new XYZ(-50, 0, 0));

            // floor cut
            add_dimension_from_point(floorView, new XYZ(300, 10, level.Elevation - 0.5), new XYZ(0, 1, 0), "62\"");
            add_dimension_from_point(floorView, new XYZ(300, 10, level.Elevation - 0.5), new XYZ(1, 0, 0), "78\"", new XYZ(0, -40, 0));

            return Result.Succeeded;
        }

        public Result setup()
        {
            setup_level();
            setup_floor();
            setup_inside_walls();
            return Result.Succeeded;
        }

    }
}
