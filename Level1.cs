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
            this.level_id = 1;
            this.level = levels[level_id];
        }

        public Result setup_level()
        {
            floorView = setup_view(ViewType.FloorPlan);
            ceilingView = setup_view(ViewType.CeilingPlan);

            // In order to get the proper 'exterior' side we have to draw the walls
            // clockwise
            XYZ[][] coords =
            {
                    new XYZ[] { new XYZ(0, 0, 0),  new XYZ(0,   368, 0) },
                    new XYZ[] { null,              new XYZ(200, 368, 0) },
                    new XYZ[] { null,              new XYZ(215, 388, 0) },
                    new XYZ[] { null,              new XYZ(265, 388, 0) },
                    new XYZ[] { null,              new XYZ(280, 368, 0) },
                    new XYZ[] { null,              new XYZ(330, 368, 0) },
                    new XYZ[] { null,              new XYZ(330, 313, 0) },
                    new XYZ[] { null,              new XYZ(468, 313, 0) },
                    new XYZ[] { null,              new XYZ(468, 0,   0) },
                    new XYZ[] { null,              new XYZ(450, 0,   0) },
                    new XYZ[] { null,              new XYZ(435, -20, 0) },
                    new XYZ[] { null,              new XYZ(385, -20, 0) },
                    new XYZ[] { null,              new XYZ(360, 0,   0) },
                    new XYZ[] { null,              new XYZ(0,   0,   0) },
                };

            WallType wType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>().FirstOrDefault(q
                => q.Name == "2x4 + Gypsum wall with Exterior");

            // Get a floor type for floor creation
            FloorType floorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>().FirstOrDefault(q
                => q.Name == "Wood Joist 10\" - Wood Finish");

            insert_exterior_walls(coords, wType, floorType);

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
                trans.Start("Basement");
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
            XYZ[][] coords =
            {
                new XYZ[] {
                    new XYZ(331, 311, 0),
                    new XYZ(331, 191, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(164, 191, 0),
                }, new XYZ[] {
                    new XYZ(250, 2.5, 0),
                    new XYZ(250, 65, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(142, 65, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(92, 115, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(92, 170, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(2, 170, 0),
                }, new XYZ[] { // Playroom south east wall
                    new XYZ(116, 0, 0),
                    new XYZ(116, 65, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(132, 72, 0),
                }, new XYZ[] {  // Toilets
                    new XYZ(174, 0, 0),
                    new XYZ(174, 65, 0),
                }, new XYZ[] { // Pantry
                    new XYZ(330, 313, 0),
                    new XYZ(285, 313, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(285, 368, 0),
                }, new XYZ[] { // Right Column
                    new XYZ(466, 191.5, 0),
                    new XYZ(456, 191.5, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(456, 177, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(466, 177, 0),
                }, new XYZ[] { // Left Column
                    new XYZ(331, 191.5, 0),
                    new XYZ(344, 191.5, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(344, 177, 0),
                }, new XYZ[] {
                    null,
                    new XYZ(331, 177, 0),
                }
            };

            insert_inside_walls(coords);

            // playroom
            add_dimension_from_point(floorView, new XYZ(20, 50, level.Elevation + 1), new XYZ(0, 1, 0), new XYZ(-60, 0, 0));
            add_dimension_from_point(floorView, new XYZ(24, 130, level.Elevation + 1), new XYZ(1, 0, 0));

            // family room
            add_dimension_from_point(floorView, new XYZ(20, 200, level.Elevation + 1), new XYZ(0, 1, 0), new XYZ(-60, 0, 0));

            // reading room
            add_dimension_from_point(floorView, new XYZ(362, 20, level.Elevation + 1), new XYZ(1, 0, 0), "213 1/2\"", new XYZ(0, -100, 0));

            // hallway
            add_dimension_from_point(floorView, new XYZ(205, 20, level.Elevation + 1), new XYZ(0, 1, 0));
            add_dimension_from_point(floorView, new XYZ(205, 20, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(0, -100, 0));

            // toilets
            add_dimension_from_point(floorView, new XYZ(140, 20, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(0, -100, 0));

            // dining
            add_dimension_from_point(floorView, new XYZ(390, 270, level.Elevation + 1), new XYZ(1, 0, 0));
            // dining + reading
            add_dimension_from_point(floorView, new XYZ(460, 20, level.Elevation + 1), new XYZ(0, 1, 0), "172 1/2\"", new XYZ(60, 0, 0));
            add_dimension_from_point(floorView, new XYZ(460, 250, level.Elevation + 1), new XYZ(0, 1, 0), "124 1/2\"", new XYZ(60, 0, 0));

            // kitchen + living room
            add_dimension_from_point(floorView, new XYZ(170, 330, level.Elevation + 1), new XYZ(1, 0, 0), new XYZ(0, 100, 0));

            // between columns
            add_dimension_from_point(floorView, new XYZ(400, 185, level.Elevation + 1), new XYZ(1, 0, 0), "107 1/2\"");

            // hallway to kitchen
            add_dimension_from_point(floorView, new XYZ(170, 129, level.Elevation + 1), new XYZ(1, 0, 0));

            return Result.Succeeded;
        }

        public Result setup()
        {
            this.level_id = 1;
            setup_level();
            setup_inside_walls();
            return Result.Succeeded;
        }

    }
}
