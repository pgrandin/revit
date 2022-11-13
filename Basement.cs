using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System.Linq;

using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;

namespace MyRevit
{
    class Basement : MyRevit.MyLevel
    {
        public Basement(UIApplication uiapp)
        {
            this.uiapp = uiapp;
            this.doc = uiapp.ActiveUIDocument.Document;
            this.level_id = 0;
            this.levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            this.level = levels[level_id];
            this.level_above = levels[2];
        }

        public Result setup_level()
        {
            floorView = setup_view(ViewType.FloorPlan);
            ceilingView = setup_view(ViewType.CeilingPlan);

            WallType wType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>().FirstOrDefault(q
                => q.Name == "Basement");

            // Get a floor type for floor creation
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(FloorType));
            FloorType floorType = collector.FirstElement() as FloorType;

            insert_exterior_walls(wType, floorType);

            // stairs ceiling opening
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
                trans.Start("ceiling openings");
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
                    doc.Create.NewDetailCurve(ceilingView, line);
                    joistCurves.Add(
                        Line.CreateBound(
                            (coords2[i][0] is null ?
                                coords2[i - 1][1].Divide(12).Add(new XYZ(0, 0, -joist_offset)) :
                                coords2[i][0].Divide(12).Add(new XYZ(0, 0, -joist_offset))
                            ),
                            coords2[i][1].Divide(12).Add(new XYZ(0, 0, -joist_offset))
                    ));
                }

                trans.Commit();
            }

            add_dimension_from_point(floorView, new XYZ(50, 50, level.Elevation), new XYZ(0, 1, 0), new XYZ(-100, 0, 0));
            return Result.Succeeded;
        }

        public Result setup_stairs()
        {
            // https://help.autodesk.com/cloudhelp/2018/ENU/Revit-API/Revit_API_Developers_Guide/Revit_Geometric_Elements/Stairs_and_Railings/Creating_and_Editing_Stairs.html

            XYZ stairsline = new XYZ(146 / 12.0, 165 / 12.0, level.Elevation);
            ElementId newStairsId = null;
            using (StairsEditScope newStairsScope = new StairsEditScope(doc, "New Stairs"))
            {

                newStairsId = newStairsScope.Start(level.Id, level_above.Id);
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Create window");
                    Line locationLine = Line.CreateBound(
                        stairsline, stairsline.Add(new XYZ(15, 0, 0)));


                    StairsRun newRun2 = StairsRun.CreateStraightRun(doc, newStairsId, locationLine, StairsRunJustification.Center);
                    // newRun2.ActualRunWidth = 10;
                    t.Commit();
                }
                newStairsScope.Commit(new MyRevit.StairsFailurePreprocessor());
            }
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

            // Metal beam
            double offset_from_floor = 80 / 12.0;
            XYZ startPoint = new XYZ(0.0, 183 / 12.0, level.Elevation + offset_from_floor);
            XYZ endPoint = new XYZ(472 / 12.0, 183 / 12.0, level.Elevation + offset_from_floor);

            FamilySymbol beamSymbol = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                    .First(q =>
                           q.Family.FamilyCategory.Name == "Structural Framing" &&
                           q.Family.Name == "W Shapes" &&
                           q.Name == "W12X26");

            using (Transaction t = new Transaction(doc))
            {
                t.Start("Activate beam");

                if (!beamSymbol.IsActive)
                {
                    beamSymbol.Activate(); // doc.Regenerate();
                }
                t.Commit();
            }

            // try to insert an instance
            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("insert beam");
                FamilyInstance fi = doc.Create.NewFamilyInstance(XYZ.Zero, beamSymbol, StructuralType.Beam);
                (fi.Location as LocationCurve).Curve = Line.CreateBound(startPoint, endPoint); ;
                tx.Commit();
            }
            return Result.Succeeded;
        }


        public Result setup_sections()
        {
            // get a ViewFamilyType for a 3D View
            ViewFamilyType viewFamilyType = (from v in new FilteredElementCollector(doc).
                                             OfClass(typeof(ViewFamilyType)).
                                             Cast<ViewFamilyType>()
                                             where v.ViewFamily == ViewFamily.ThreeDimensional
                                             select v).First();

            using (Transaction t = new Transaction(doc, "Create view"))
            {
                t.Start();

                // Create the 3d view
                View3D view = View3D.CreateIsometric(doc, viewFamilyType.Id);

                // Set the name of the view
                view.Name = "Basement Section Box";

                // Set the name of the transaction
                // A transaction can be renamed after it has been started
                t.SetName("Create view " + view.Name);

                // Create a new BoundingBoxXYZ to define a 3D rectangular space
                BoundingBoxXYZ boundingBoxXYZ = new BoundingBoxXYZ();

                // Set the lower left bottom corner of the box
                // Use the Z of the current level.
                // X & Y values have been hardcoded based on this RVT geometry
                boundingBoxXYZ.Min = new XYZ(-2, 2, level.Elevation - 5);

                boundingBoxXYZ.Max = new XYZ(500 / 12, 500 / 12, 50);

                // Apply this bouding box to the view's section box
                view.SetSectionBox(boundingBoxXYZ);

                t.Commit();
            }
            return Result.Succeeded;
        }


        public Result setup_inside_walls()
        {

            insert_inside_walls();

            // workshop
            add_dimension_from_point(floorView, new XYZ(50, 50, level.Elevation), new XYZ(0, 1, 0));

            // bathroom
            add_dimension_from_point(floorView, new XYZ(250, 50, level.Elevation), new XYZ(0, 1, 0), "93 1/2\"");
            add_dimension_from_point(floorView, new XYZ(250, 50, level.Elevation), new XYZ(1, 0, 0), "107 1/2\"", new XYZ(0, -100, 0));

            // guest bedroom
            add_dimension_from_point(floorView, new XYZ(450, 140, level.Elevation), new XYZ(0, 1, 0), "108 1/2\"", new XYZ(100, 0, 0));
            add_dimension_from_point(floorView, new XYZ(450, 140, level.Elevation), new XYZ(1, 0, 0), "103\"");
            add_dimension_from_point(floorView, new XYZ(350, 40, level.Elevation), new XYZ(1, 0, 0), "96\"");
            add_dimension_from_point(floorView, new XYZ(340, 40, level.Elevation), new XYZ(0, 1, 0), "89.5\"");
            add_dimension_from_point(floorView, new XYZ(360, 40, level.Elevation), new XYZ(0, 1, 0), "173\"", new XYZ(220, 0, 0));
            add_dimension_from_point(floorView, new XYZ(360, 98, level.Elevation), new XYZ(1, 0, 0), "161\"", new XYZ(0, -148, 0));

            // HT
            add_dimension_from_point(floorView, new XYZ(50, 200, level.Elevation), new XYZ(0, 1, 0), "175\"");
            add_dimension_from_point(floorView, new XYZ(165, 220, level.Elevation), new XYZ(0, 1, 0), "255\"");
            add_dimension_from_point(floorView, new XYZ(310, 270, level.Elevation), new XYZ(0, 1, 0), "172\"");
            add_dimension_from_point(floorView, new XYZ(180, 300, level.Elevation), new XYZ(1, 0, 0), new XYZ(0, 100, 0));

            // cellar
            add_dimension_from_point(floorView, new XYZ(450, 270, level.Elevation), new XYZ(0, 1, 0), new XYZ(100, 0, 0));
            add_dimension_from_point(floorView, new XYZ(450, 270, level.Elevation), new XYZ(1, 0, 0));

            // hallway
            add_dimension_from_point(floorView, new XYZ(300, 120, level.Elevation), new XYZ(1, 0, 0), "226\"");
            add_dimension_from_point(floorView, new XYZ(300, 120, level.Elevation), new XYZ(0, 1, 0), "36\"");

            return Result.Succeeded;
        }

        public Result setup_doors()
        {
            XYZ[] doors_locations = {
                new XYZ(160 / 12.0, 103.5 / 12.0, (double) DoorOperations.Should_flip),
                new XYZ(275 / 12.0, 103.5 / 12.0, (double) DoorOperations.Should_flip),
                new XYZ(320 / 12.0, 103.5 / 12.0, (double) (DoorOperations.Should_flip | DoorOperations.Should_rotate)),
                new XYZ(312 / 12.0, 166.0 / 12.0, (double) DoorOperations.Should_flip),
            };

            return insert_doors(doors_locations, level);
        }


        public Result setup_windows()
        {
            XYZ[] windows_locations = {
                new XYZ(398 / 12.0, 313 / 12.0, (double) DoorOperations.None),
                new XYZ(468 / 12.0, 136 / 12.0, (double) DoorOperations.None),
                new XYZ(  0 / 12.0,  98 / 12.0, (double) DoorOperations.None),
            };

            return insert_windows(windows_locations, level);
        }

        public Result setup()
        {
            setup_level();
            setup_stairs();
            setup_joists();
            setup_sections();
            setup_inside_walls();
            setup_doors();
            setup_windows();
            ExportToImage(floorView);
            return Result.Succeeded;
        }

    }
}
