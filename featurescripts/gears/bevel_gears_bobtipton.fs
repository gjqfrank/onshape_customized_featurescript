FeatureScript 1605;
import(path : "onshape/std/geometry.fs", version : "1605.0");
import(path : "9a614dbfeebb7542711bad1d/a17ffc4446961b2fbadc5391/cc79bc30893b7e15993a92e5", version : "0c5898468dfd723b5e96e4b6");
import(path : "06522475311e9e09c6beb0ca/b20f5f5b9703a38c1eb89787/e574dd798941afe4c9a34423", version : "b70783136faa7f8a8db28e75");
import(path : "9a614dbfeebb7542711bad1d/a17ffc4446961b2fbadc5391/e10a863cc67086d3cfccf121", version : "fcb543c0763ae53e5747ae76");

annotation { "Feature Type Name" : "Bevel Gears",
             "Feature Type Description" : 
             "Copyright, Concurrent Engineering Tools all rights reserved.<br><br>" ~
             "This version is 'Charity Ware'. Please read the document description and donate if you use it often.<br><br>" ~
             "Creates one pair of Spherical Involute Bevel gears.<br><br>" ~
             "This feature may be slow if all options are used. It's best practice to finish the gear geometry before applying fillets and addendums or dedendums.<br><br>" ~
             "Gears are sized based on tooth count and the pitch radius of Gear A. The actual radius of the gear tooth tips is derived from this.<br><br>" ~
             "The algorithm uses iterative collision testing to find the base radii where the teeth of each gear meet within system tolerance.<br><br>" ~
             "The feature calls opBevelGears to perform all operations. You may call this function directly in your own features.<br><br>"
             }
export const bevelGearsPublic = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Cone Point",  "Filter" : EntityType.VERTEX,  "MaxNumberOfPicks" : 1,
        "Description" : "Point which sets the intersection of the gear axes. The tip of both cones."}
        definition.conePoint is Query;
        
        annotation { "Name" : "Axis A Point", "Description" : "Point which sets the direction of gear A's axis.", "Filter" : EntityType.VERTEX, "MaxNumberOfPicks" : 1 }
        definition.gearPointA is Query;

        annotation { "Name" : "Axis B Point", "Description" : "Point which sets the direction of gear B's axis.", "Filter" : EntityType.VERTEX, "MaxNumberOfPicks" : 1 }
        definition.gearPointB is Query;
        
        annotation { "Name" : "# Teeth A", "Description" : "Number of teeth on Gear A." }
        isInteger(definition.numTeethA, NUM_TEETH_BOUNDS);
        
        annotation { "Name" : "# Teeth B", "Description" : "Number of teeth on Gear B." }
        isInteger(definition.numTeethB, NUM_TEETH_BOUNDS);
        
        annotation { "Name" : "Pitch Diameter A", "Description" : "Pitch diameter of Gear A. <br>This is not the actual diameter of the gear." }
        isLength(definition.baseDia, BASE_DIAMETER_BOUNDS);
        
        annotation { "Name" : "Shaft Diameter A", "Description" : "Shaft diameter of Gear A." }
        isLength(definition.shaftDiaA, SHAFT_DIAMETER_BOUNDS);

        annotation { "Name" : "Shaft Diameter B", "Description" : "Shaft diameter of Gear B." }
        isLength(definition.shaftDiaB, SHAFT_DIAMETER_BOUNDS);

        annotation { "Name" : "Tooth width", "Description" : "The distance from the outer edge of a gear tooth to the inner edge, measured along a line through the Cone Point." }
        isLength(definition.toothWidth, TOOTH_WIDTH_BOUNDS);

        annotation { "Name" : "Helical shift", "Default" : 0.0,
        "Description" : "If this value is non zero, it will create a helical gear. The end face of the tooth is rotated by the tooth angle times this fraction." }
        isReal(definition.helicalShift, GEAR_HELICAL_SHIFT_BOUNDS);
        
        annotation { "Name" : "V Helix", "Default" : false,
        "Description" : "If set, the helix reverses rotation at the center to form a V helix or herringbone." }
        definition.isVHelix is boolean;        

        annotation { "Name" : "Inner Add",
        "Description" : "Inner addendum of gear A and outer dedendum of gear B" }
        isLength(definition.innerAddA, GAP_BOUNDS);

        annotation { "Name" : "Outer Ded",
        "Description" : "Outer dedendum of gear A and inner addendum of gear B" }
        isLength(definition.outerDedA, GAP_BOUNDS);

        annotation { "Name" : "Clearance",
        "Description" : "Clearance between the cap of gear A and the addendum radius of gear B." }
        isLength(definition.clearance, CLEARANCE_BOUNDS);

        annotation { "Name" : "Inner Add Fillet Radius",
        "Description" : "This fillet is applied to base edges of gear A's teeth and cap edges of gear B's teeth." }
        isLength(definition.innerAddFilletRadius, FILLET_RADIUS_BOUNDS);
        
        annotation { "Name" : "Outer Ded Fillet Radius",
        "Description" : "This fillet is applied to cap edges of gear A's teeth and base edges of gear B's teeth." }
        isLength(definition.outerDedFilletRadius, FILLET_RADIUS_BOUNDS);
        
        annotation { "Name" : "Cap End Fillet Radius",
        "Description" : "This fillet is applied to the start and end cap edges of both gears." }
        isLength(definition.capEndFilletRadius, FILLET_RADIUS_BOUNDS);

        annotation { "Name" : "Phase", "Default" : 0.0,
        "Description" : "Rotates each gear by a fraction of one tooth angle. Useful for testing engagement." }
        isReal(definition.phase, GEAR_PHASE_BOUNDS);    }
    {
        opBevelGears(context, id, definition);
    });

annotation { "Feature Type Name" : "Spur Gear" }
export const spurGearPublic = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Base Circle", "Filter" : EntityType.EDGE && GeometryType.CIRCLE, "MaxNumberOfPicks" : 1,
        "Description" : "This circle defines the outer diameter of the gear tooth caps. The base radius is offset inward." }
        definition.baseCircleQuery is Query;

        annotation { "Name" : "# Teeth", "Description" : "Number of teeth on gear." }
        isInteger(definition.numTeeth, NUM_TEETH_BOUNDS);
        
        annotation { "Name" : "Thickness",
        "Description" : "Thickness of the gear." }
        isLength(definition.thickness, LENGTH_BOUNDS);
        annotation { "Name" : "Opposite direction", "UIHint" : UIHint.OPPOSITE_DIRECTION }
        definition.oppositeDirection is boolean;

        annotation { "Name" : "Shaft Dia",
        "Description" : "Diameter of the gear's shaft hole." }
        isLength(definition.shaftDia, SHAFT_DIAMETER_BOUNDS);

        annotation { "Name" : "Helical shift", "Default" : 0.0,
        "Description" : "If this value is non zero, it will create a helical gear. The end face of the tooth is rotated by the tooth angle times this fraction." }
        isReal(definition.helicalShift, GEAR_HELICAL_SHIFT_BOUNDS);
        
        annotation { "Name" : "V Helix", "Default" : false,
        "Description" : "If set, the helix reverses rotation at the center to form a V helix or herringbone." }
        definition.isVHelix is boolean;        

        annotation { "Name" : "Inner Add",
        "Description" : "Inner addendum of gear A and outer dedendum of gear B" }
        isLength(definition.innerAdd, GAP_BOUNDS);

        annotation { "Name" : "Outer Ded",
        "Description" : "Outer dedendum of gear A and inner addendum of gear B" }
        isLength(definition.outerDed, GAP_BOUNDS);

        annotation { "Name" : "Clearance",
        "Description" : "Clearance between the cap of gear A and the addendum radius of gear B." }
        isLength(definition.clearance, CLEARANCE_BOUNDS);

        annotation { "Name" : "Inner Add Fillet Radius",
        "Description" : "This fillet is applied to base edges of gear A's teeth and cap edges of gear B's teeth." }
        isLength(definition.innerAddFilletRadius, FILLET_RADIUS_BOUNDS);
        
        annotation { "Name" : "Outer Ded Fillet Radius",
        "Description" : "This fillet is applied to cap edges of gear A's teeth and base edges of gear B's teeth." }
        isLength(definition.outerDedFilletRadius, FILLET_RADIUS_BOUNDS);
        
        annotation { "Name" : "Cap End Fillet Radius",
        "Description" : "This fillet is applied to the start and end cap edges of both gears." }
        isLength(definition.capEndFilletRadius, FILLET_RADIUS_BOUNDS);
    }
    {
        opSpurGear(context, id, definition);
    });

annotation { "Feature Type Name" : "Matching Spur Gear" }
export const matchingSpurGearPublic = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Base Gear", "Filter" : EntityType.BODY, "MaxNumberOfPicks" : 1,
        "Description" : "The gear to match." }
        definition.baseGearQuery is Query;
        
        annotation { "Name" : "Ref Point", "Filter" : EntityType.VERTEX, "MaxNumberOfPicks" : 1,
        "Description" : "A vertex to define the direction for the second axis. The length is calculated so the gears mesh correctly." }
        definition.refVertex is Query;

        annotation { "Name" : "# Teeth", "Description" : "Number of teeth on gear." }
        isInteger(definition.numTeeth, NUM_TEETH_BOUNDS);
        
        annotation { "Name" : "Shaft Dia" }
        isLength(definition.shaftDia, SHAFT_DIAMETER_BOUNDS);
        
        annotation { "Name" : "Outer Ded" }
        isLength(definition.outerDed, GAP_BOUNDS);

        annotation { "Name" : "Inner Add" }
        isLength(definition.innerAdd, GAP_BOUNDS);

        annotation { "Name" : "Clearance",
        "Description" : "Clearance between the cap of gear A and the addendum radius of gear B." }
        isLength(definition.clearance, CLEARANCE_BOUNDS);

        annotation { "Name" : "Inner Add Fillet Radius",
        "Description" : "This fillet is applied to base edges of gear A's teeth and cap edges of gear B's teeth." }
        isLength(definition.innerAddFilletRadius, FILLET_RADIUS_BOUNDS);
        
        annotation { "Name" : "Outer Ded Fillet Radius",
        "Description" : "This fillet is applied to cap edges of gear A's teeth and base edges of gear B's teeth." }
        isLength(definition.outerDedFilletRadius, FILLET_RADIUS_BOUNDS);
    }
    {
        opMatchingSpurGear(context, id, definition);
    });
    
annotation { "Feature Type Name" : "Spur Gear Pair" }
export const spurGearPairPublic = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Reference", "Filter" : (EntityType.EDGE  && GeometryType.LINE) || EntityType.VERTEX, "MaxNumberOfPicks" : 2,
        "Description" : "A line or two points which places the centers of the gears. These will be the origins for the gear axes. If using a line, the two end vertices are used." }
        definition.refEntities is Query;

        annotation { "Name" : "Thickness",
        "Description" : "Thickness of the gears." }
        isLength(definition.thickness, LENGTH_BOUNDS);

        annotation { "Name" : "Opposite direction", "UIHint" : UIHint.OPPOSITE_DIRECTION }
        definition.oppositeDirection is boolean;

        annotation { "Name" : "# Teeth A",
        "Description" : "Number of teeth on gear A." }
        isInteger(definition.numTeethA, NUM_TEETH_BOUNDS);
        
        annotation { "Name" : "# Teeth B",
        "Description" : "Number of teeth on gear B." }
        isInteger(definition.numTeethB, NUM_TEETH_BOUNDS);
        
        annotation { "Name" : "Shaft Dia A",
        "Description" : "Shaft diameter of gear A." }
        isLength(definition.shaftDiaA, SHAFT_DIAMETER_BOUNDS);

        annotation { "Name" : "Shaft Dia B",
        "Description" : "Shaft diameter of gear B." }
        isLength(definition.shaftDiaB, SHAFT_DIAMETER_BOUNDS);

        annotation { "Name" : "Helical shift", "Default" : 0.0,
        "Description" : "If this value is non zero, it will create a helical gear. The end face of the tooth is rotated by the tooth angle times this fraction." }
        isReal(definition.helicalShift, GEAR_HELICAL_SHIFT_BOUNDS);
        
        annotation { "Name" : "V Helix", "Default" : false,
        "Description" : "If set, the helix reverses rotation at the center to form a V helix or herringbone." }
        definition.isVHelix is boolean;        

        annotation { "Name" : "Inner Add",
        "Description" : "Inner addendum of gear A and outer dedendum of gear B" }
        isLength(definition.innerAddA, GAP_BOUNDS);

        annotation { "Name" : "Outer Ded",
        "Description" : "Outer dedendum of gear A and inner addendum of gear B" }
        isLength(definition.outerDedA, GAP_BOUNDS);

        annotation { "Name" : "Clearance",
        "Description" : "Clearance between the cap of gear A and the addendum radius of gear B." }
        isLength(definition.clearance, CLEARANCE_BOUNDS);

        annotation { "Name" : "Inner Add Fillet Radius",
        "Description" : "This fillet is applied to base edges of gear A's teeth and cap edges of gear B's teeth." }
        isLength(definition.innerAddFilletRadius, FILLET_RADIUS_BOUNDS);
        
        annotation { "Name" : "Outer Ded Fillet Radius",
        "Description" : "This fillet is applied to cap edges of gear A's teeth and base edges of gear B's teeth." }
        isLength(definition.outerDedFilletRadius, FILLET_RADIUS_BOUNDS);
        
        annotation { "Name" : "Cap End Fillet Radius",
        "Description" : "This fillet is applied to the start and end cap edges of both gears." }
        isLength(definition.capEndFilletRadius, FILLET_RADIUS_BOUNDS);

        annotation { "Name" : "Phase", "Default" : 0.0,
        "Description" : "Rotates each gear by a fraction of one tooth angle. Useful for testing engagement." }
        isReal(definition.phase, GEAR_PHASE_BOUNDS);

    }
    {
        opSpurGearPair(context, id, definition);
    });

