using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevit
{
    class Garage : MyRevit.MyLevel
    {
        public Garage(Document doc)
        {
            this.doc = doc;
            this.levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            this.level_id = 1;
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

            insert_exterior_walls(wType, null);

            add_dimension_from_point(floorView, new XYZ(100, -100, level.Elevation + 0.5), new XYZ(1, 0, 0), "364\"");
            add_dimension_from_point(floorView, new XYZ(100, -100, level.Elevation + 0.5), new XYZ(0, 1, 0), "239\"");

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

            return Result.Succeeded;
        }

        public Result setup()
        {
            setup_level();
            // setup_floor();
            setup_inside_walls();
            return Result.Succeeded;
        }

    }
}
