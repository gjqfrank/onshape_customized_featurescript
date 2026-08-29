FeatureScript 1803;
import(path : "onshape/std/geometry.fs", version : "1803.0");
IconNamespace::import(path : "f32b895e598633f321b326a2/2e340c6b644f40f23b155212/7e500c06a0dec1e9c9fd535d", version : "953066f0945050e2f85b753b");
ImageNamespace::import(path : "f32b895e598633f321b326a2/2e340c6b644f40f23b155212/15bd11c10e5a37ca8a9a6290", version : "e0b02f2877fff73f56442bc4");
export import(path : "f32b895e598633f321b326a2/807dc517648c47be7752daf4/b9ac907482a1fba4731bd53c", version : "e10878ab395e56f3b36c7224");

/*
    "Gear Lab" Featurescript
    Anthony Lu 
    
    V1.1.04 - November 03, 2023
        * By request, increased maximum tooth count to 1000.
    
    Please report any issues you encounter in the forum thread here: https://forum.onshape.com/discussion/18686/gear-lab-cylindrical-bevel-face-gears.
    Or just shoot me a message through the OnShape forum interface (uid: antlu65). Will try to resolve issues as quickly as possible.
    
    Thanks to:
    * "OpenSCAD Getriebe Bibliothek" - Dr. Jörg Janssen (https://www.thingiverse.com/thing:1604369)
    * "Spur Gear" Featurescript - Neil Cooke, PTC (https://cad.onshape.com/documents/0023de306780bd6153871aa4/v/8fea747c8a15e3e9f2e607bb/e/d26f32003891756b0ecb81f1)
    
    References:
    * Litvin F. L., Fuentes A., "Gear Geometry and Applied Theory Second Edition", 2004
    * Vullo V., "Gears Volume 1: Geometric and Kinematic Design", Springer Series in Solid and Structural Mechanics, 2020
    * Radzevich S. P., "Handbook of Practical Gear Design and Manufacture Second Edition", 2012
    * Ligata H., Zhang H. H., "Geometry Definition and Contact Analysis of Spherical Involute Straight Bevel Gears", 2011
    * Shunmugam, M. S., Subba, B., R., Jayaprakash, V., “Establishing Gear Tooth Surface Geometry and Normal Deviation”, Mechanisms and Machine Theory, V33, No 5, pp 525-534, 1998
*/

annotation {
    "Feature Type Name" : "Gear Lab",
    "Editing Logic Function" : "onFeatureChange",
    "Manipulator Change Function" : "onManipulatorChange",
    "Feature Name Template" : "#" ~ GEARLAB_PARAM_DESC,
    "Icon" : IconNamespace::BLOB_DATA,
    "Description Image" : ImageNamespace::BLOB_DATA,
    "Feature Type Description" : "Create cylindrical, bevel, and face gears with straight, helical, spiral, and herringbone teeth patterns. Specify gear module, teeth count, bevel angle, helix angle, etc., or inherit values from and align to the pitch surface of another gear created with this custom feature."
}
export const GearLabFeature = defineFeature(function(context is Context, id is Id, def is map)
    precondition
    {
        annotation { "Name" : "Build Method", "UIHint" : UIHint.HORIZONTAL_ENUM }
        def.buildMethod is GearBuildMethod;
        
        // Build Method: Manual
        if (def.buildMethod == GearBuildMethod.MANUAL)
        {
            annotation { "Name" : "Select Alignment Geometry", "Filter" : EntityType.VERTEX || GeometryType.PLANE || GeometryType.CIRCLE, "MaxNumberOfPicks" : 2 }
            def.buildManual_alignGeometry is Query;
            annotation { "Name" : "Flip Build Direction", "UIHint" : UIHint.OPPOSITE_DIRECTION }
            def.buildManual_alignFlip is boolean;
        }
        
        // Build Method: Inherit
        else if (def.buildMethod == GearBuildMethod.INHERIT)
        {
            annotation { "Name" : "Select Parent Gear", "Filter" : EntityType.BODY, "MaxNumberOfPicks" : 1 }
            def.buildInherit_parentGear is Query;
            annotation { "Name" : "Child Align Method", "UIHint" : [UIHint.HORIZONTAL_ENUM, UIHint.REMEMBER_PREVIOUS_VALUE] }
            def.buildInherit_alignMethod is BuildInheritAlignMethod;
            if (def.buildInherit_alignMethod == BuildInheritAlignMethod.SET_ANGLE)
            {
                annotation { "Name" : "Align Angle" }
                isAngle(def.buildInherit_alignAngle, ANGLE_360_ZERO_DEFAULT_BOUNDS);
            }
            else if (def.buildInherit_alignMethod == BuildInheritAlignMethod.ALIGN_TO_GEOMETRY)
            {
                annotation { "Name" : "Align Child Gear to Geometry", "Filter" : EntityType.VERTEX || GeometryType.PLANE || GeometryType.LINE, "MaxNumberOfPicks" : 1 }
                def.buildInherit_alignGeometry is Query;
                annotation { "Name" : "Flip Alignment Direction", "UIHint" : UIHint.OPPOSITE_DIRECTION }
                def.buildInherit_alignFlip is boolean;
            }
            annotation { "Name" : "Shaft Angle" }
            isAngle(def.buildInherit_shaftAngle, { (degree) : [0, 0, 180] } as AngleBoundSpec );
        }
        
        // General Settings.
        annotation { "Name" : "Teeth"}//, "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        isInteger(def.z, GEAR_TEETH_BOUNDS);

        if (def.buildMethod == GearBuildMethod.MANUAL)
        {
            annotation { "Name" : "Module", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isLength(def.m, GEAR_MODULE_BOUNDS);
            annotation { "Name" : "Pressure Angle", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE, "Description" : "This is the normal pressure angle (distinguished from transverse pressure angle) and is unaffected by changes in the helix angle." }
            isAngle(def.pa, GEAR_PRESSUREANGLE_BOUNDS);
            annotation { "Name" : "Bevel Angle", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE, "Description" : "The angle between the gear's axis and its pitch surface. BA = 0deg -> cylindrical gear; 0deg < BA < 90deg -> bevel gear; BA = 90deg -> face gear."}
            isAngle(def.ba, GEAR_BEVELANGLE_BOUNDS);
            annotation { "Name" : "Helix Angle", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isAngle(def.ha, GEAR_HELIXANGLE_BOUNDS);
            annotation { "Name" : "Herringbone", "UIHint" : [UIHint.REMEMBER_PREVIOUS_VALUE, UIHint.DISPLAY_SHORT] }
            def.herringbone is boolean;
            annotation { "Name" : "Tooth Width", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE, "Description" : "The tooth width, as a multiple of the gear module, as measured along the pitch surface." }
            isReal(def.tooth_width, GEAR_TOOTHWIDTH_BOUNDS);
        }

        annotation { "Name" : "Base Depth", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isReal(def.base_depth, GEAR_DEPTH_BOUNDS);
        annotation { "Name" : "Internal Teeth", "UIHint" : [UIHint.REMEMBER_PREVIOUS_VALUE, UIHint.DISPLAY_SHORT] }
        def.internal is boolean;
        if (def.internal)
        {
            annotation { "Name" : "Ring Depth", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isReal(def.ring_depth, GEAR_DEPTH_BOUNDS);
        }
        
        annotation { "Name" : "Bore", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        def.bore is boolean;
        if (def.bore)
        {
            annotation { "Group Name" : "Bore Settings", "Driving Parameter" : "bore", "Collapsed By Default" : false }
            {
                annotation { "Name" : "Diameter", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
                isLength(def.bore_dia, GEAR_BOREDIAMETER_BOUNDS);
                annotation { "Name" : "Keyway Height", "UIhint" : UIHint.REMEMBER_PREVIOUS_VALUE }
                isLength(def.bore_keyh, GEAR_BOREKEYWAY_BOUNDS);
                annotation { "Name" : "Keyway Width", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
                isLength(def.bore_keyw, GEAR_BOREKEYWAY_BOUNDS);
            }
        }

        if (def.buildMethod == GearBuildMethod.MANUAL)
        {
            annotation { "Name" : "Tooth Chamfer", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            def.chamfer is boolean;
            if (def.chamfer)
            {
                annotation { "Group Name" : "Chamfer Settings", "Driving Parameter" : "chamfer", "Collapsed By Default" : false }
                {
                    annotation { "Name" : "Style", "UIHint" : [UIHint.REMEMBER_PREVIOUS_VALUE, UIHint.HORIZONTAL_ENUM] }
                    def.chamfer_style is GearChamferStyle;
                    annotation { "Name" : "Distance", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
                    isReal(def.chamfer_df, GEAR_CHAMFERFACTOR_BOUNDS);
                }
            }
            
            annotation { "Name" : "Root Fillet", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            def.fillet is boolean;
            if (def.fillet)
            {
                annotation { "Group Name" : "Fillet Settings", "Driving Parameter" : "fillet", "Collapsed By Default" : false }
                {
                    annotation { "Name" : "Distance" }
                    isReal(def.fillet_df, GEAR_FILLETFACTOR_BOUNDS);
                }
            }
        }

        // Advanced Settings.
        annotation { "Group Name" : "Advanced Settings" }
        {
            annotation { "Name" : "Adjust Angle", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isAngle(def.adjustAngle, ANGLE_360_ZERO_DEFAULT_BOUNDS);
            annotation { "Name" : "Side Reduction", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isReal(def.cs, GEAR_SIDEFACTOR_BOUNDS);
            annotation { "Name" : "Root Clearance", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isReal(def.cd, GEAR_ROOTFACTOR_BOUNDS);
            annotation { "Name" : "Tip Extension", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isReal(def.ca, GEAR_TIPFACTOR_BOUNDS);
            annotation { "Name" : "Minimum Land", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isReal(def.cl, GEAR_LANDFACTOR_BOUNDS);
            annotation { "Name" : "Involute Steps", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isInteger(def.invsteps, GEAR_INVOLUTESTEPS_BOUNDS);
        }

        // Debug View.
        annotation { "Group Name" : "Debug View" , "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        {
            annotation { "Name" : "Coord Systems", "UIHint" : UIHint.DISPLAY_SHORT }
            def.debug_drawCoordSystems is boolean;
            annotation { "Name" : "Pitch Surfaces", "UIHint" : UIHint.DISPLAY_SHORT }
            def.debug_drawPitchSurfaces is boolean;
        }
    }
    {
        GearLabFeature_Main(context, id, def);
    });