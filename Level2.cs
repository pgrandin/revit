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
            this.level_id = 2;
            this.level = levels[level_id];
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
            setup_inside_walls();
            return Result.Succeeded;
        }

    }
}
