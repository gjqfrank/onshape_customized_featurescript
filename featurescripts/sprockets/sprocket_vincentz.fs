FeatureScript 1174;
import(path : "onshape/std/geometry.fs", version : "1174.0");

export const NUMBER_OF_TEETH =
{
    (unitless) : [1, 16, 1e5]
} as IntegerBoundSpec;

export const BORE_DIAMETER =
{
    (inch) : [0, 0.5, 2],
    (meter)      : 0.025,
    (centimeter) : 2.5,
    (millimeter) : 25.0,
    (foot)       : 0.1,
    (yard)       : 0.025
} as LengthBoundSpec;

export const DEPTH_BOUNDS =
{
    (inch) : [0, 0.5, 1e5],
    (meter)      : 0.025,
    (centimeter) : 2.5,
    (millimeter) : 25.0,
    (foot)       : 0.1,
    (yard)       : 0.025
} as LengthBoundSpec;


// Enum that represents the associated string chosen from the Chain No. Option precondition
export enum ANSI_Number_Choice
{
    annotation { "Name" : "Chain No. 25" }
    ANSI_25,
    annotation { "Name" : "Chain No. 35" }
    ANSI_35,
    annotation { "Name" : "Chain No. 40" }
    ANSI_40,
    annotation { "Name" : "Chain No. 41" }
    ANSI_41,
    annotation { "Name" : "Chain No. 50" }
    ANSI_50,
    annotation { "Name" : "Chain No. 60" }
    ANSI_60,
    annotation { "Name" : "Chain No. 80" }
    ANSI_80,
    annotation { "Name" : "Chain No. 100" }
    ANSI_100,
    annotation { "Name" : "Chain No. 120" }
    ANSI_120,
    annotation { "Name" : "Chain No. 120" }
    ANSI_140,
    annotation { "Name" : "Chain No. 120" }
    ANSI_160,
    annotation { "Name" : "Chain No. 120" }
    ANSI_180,
    annotation { "Name" : "Chain No. 120" }
    ANSI_200,
    annotation { "Name" : "Chain No. 120" }
    ANSI_240,
}

// Functions that sets the Pitch and Roller Diameter based off Chain No. for Standard Series Roller Chain
// Values are based off Chain Data provided by Brewton Iron Works Inc "Sprocket Tooth Dimension"
function    Set_P_and_Dr(ANSI_Choice)
{
    var arr = makeArray(2, 0);
    if (ANSI_Choice == ANSI_Number_Choice.ANSI_35)
        arr = [3/8, 0.200];
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_40)
        arr = [1/2, 0.312];
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_41)
        arr = [1/2, 0.306];
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_50)
        arr = [5/8, 0.400];
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_60)
        arr = [3/4, 0.469];  
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_80)
        arr = [1, 0.625];  
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_100)
        arr = [1.25, 0.750];  
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_120)
        arr = [1.5, 0.875];  
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_140)
        arr = [1.75, 1.000];  
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_160)
        arr = [2, 1.125];  
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_180)
        arr = [2.25, 1.406];  
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_200)
        arr = [2.5, 1.562];  
    else if (ANSI_Choice == ANSI_Number_Choice.ANSI_240)
        arr = [3, 1.875];
    else
        arr = [1/4, 0.130];
    return (arr);
}

function    Extrude_direction(face, direction)
{
    if (direction == true)
        face.normal = -face.normal;
    return (face);
}

annotation { "Feature Type Name" : "Sprocket" }
export const myFeature = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {   
        annotation { "Name" : "Surface", "Filter" : GeometryType.PLANE || EntityType.FACE, "MaxNumberOfPicks" : 1}
        definition.plane is Query;
        
        annotation { "Name" : "Choose Center" }
        definition.CenterPoint is boolean;
        
        if (definition.CenterPoint == true)
        {
            annotation { "Name" : "Center of Sprocket", "Filter" : EntityType.VERTEX, "MaxNumberOfPicks" : 1 }
            definition.center is Query;
        }
        
        annotation { "Name" : "Sprocket ANSI Number" }
        definition.ChainNum is ANSI_Number_Choice;
        
        annotation { "Name" : "Number of Teeth" }
        isInteger(definition.myCount, NUMBER_OF_TEETH);
        
        annotation { "Name" : "Bore Diameter" }
        isLength(definition.BoreDiameter, BORE_DIAMETER);
        
        annotation { "Name" : "Depth" }
        isLength(definition.Depth, DEPTH_BOUNDS);
        
        annotation { "Name" : "Opposite direction", "UIHint" : "OPPOSITE_DIRECTION" }
        definition.oppositeDirection is boolean;
    }
    {
        // Calculations based off of the design equations for a sprocket
        const Bore_D = definition.BoreDiameter;
        const N = definition.myCount;                                   // Number of Teeth
        
        var arr = Set_P_and_Dr(definition.ChainNum);
        var P = arr[0];                                                 // Pitch 
        var Dr = arr[1];                                                // Roller Diameter
        const PD = P/sin(180/N *degree);                                // Pitch Diameter
        const y = P / (2*tan(180/N*degree));                        
        const z = y*((PD-Dr)/PD);
        const x = P*z/y;
        const Ds = (1.005 * Dr) + 0.003;                                // incoporates some tolerance 
        const A = (35 + (60/N));                                        // Angle between 
        const B = (18 - (56/N));
        const E = 2*((1.3025*Dr) + 0.0015);
        const ab = 1.4*Dr;
        const V = ab*(sin(180/N*degree));                   
        const F_part1 = 0.8*cos((18 - (56/N))*degree);      
        const F_part2 = 1.4*cos((17 - (64/N))*degree);      
        const F = 2*(Dr*(F_part1 + F_part2 - 1.3025) - 0.0015);
        const M = 0.8 * Dr * cos((35 + (65/N)) * degree);
        const T = 0.8 * Dr * sin((35 + (65/N)) * degree);
        const W = 1.4 * Dr * cos(180/N *degree);
        
        // Pulls the Selected Plane and point into variables to be used for later
        var selected_face = evPlane(context, {"face" : definition.plane});
        var point_o = selected_face.origin;
        if (definition.CenterPoint == true)
            point_o = evVertexPoint(context, {"vertex" : definition.center});
        selected_face = Extrude_direction(selected_face, definition.oppositeDirection);
        
        // Runs a quick check to see if the selected point is co-planer to selected plane
        if (isPointOnPlane(point_o, selected_face) == false)
            throw ("Point not on selected face");

        // Creates a new plane based off of our selected point and normal vector of our selected plane
        var plane1 = plane(point_o, selected_face.normal);
        
        // Sets our point_o and plane1 as the world's absolute coordinates so we don't have to offset...
        //... all of our calculations and points by the new point
        // Or you can offset all points with this ...
        //... var center2D = vector(point_o[0].value, point_o[1].value);
        worldToPlane3D(plane1, point_o);
        
        var sketch1 = newSketchOnPlane(context, id + "sketch1", {
                "sketchPlane" : plane1 //qCreatedBy(makeId("Top"), EntityType.FACE)
        });

        // Construction line from origin of Top Plane to point A (center of driving shaft)
        var point_ax = 0;
        var point_ay = PD/2;
        var vec_a = vector(point_ax, point_ay);
        skLineSegment(sketch1, "line_oa", {
                "start" : vector(0, 0) * inch,
                "end" : vec_a * inch,
                "construction" : true
        });
        
        // Construction line from point a to c using absolute vector corodinates
        var point_cx = M;
        var point_cy = T + point_ay;
        var vec_c = vector(point_cx, point_cy);
        skLineSegment(sketch1, "line_ac", {
                "start" : vec_a * inch,
                "end" : vec_c * inch,
                "construction": true
        });
    
        // Trignometric calculations to find points y and z which are the points that...
        //... defines the line segment perpendicular to circle_E
        const C = 90 - A;
        const alpha = B + C;
        const theta = 90 - alpha;
        var adjust_x = (E/2) / cos(theta*degree);
        var point_zx = point_cx - adjust_x;
        var point_zy = point_cy;
        var vec_z = vector(point_zx, point_zy);
        
        var adjust_y = (E/2) * sin(theta*degree);
        var adjust_x2 = adjust_x - adjust_y/tan(alpha*degree);
        var point_yx = point_cx - adjust_x2;
        var point_yy = point_cy - adjust_y;
        var vec_y = vector(point_yx, point_yy);
        
        // Positive and negative line versions are drawn to make sketch appear as a mirror about y-axis
        skLineSegment(sketch1, "line_yz_neg", {
                "start" : vec_y * inch,
                "end" : vec_z * inch
        });
        skLineSegment(sketch1, "line_yz_pos", {
                "start" : vector(-point_yx, point_yy) * inch,
                "end" : vector(-point_zx, point_zy) * inch
        });
        
        // Trignometric calucaltions to find points of u and b:
        //...point u = tangent point of circle_Ds and circle_E (bottom of tooth)
        //...point b = center of circle F (top of tooth)
        var point_ux = point_ax - (Ds/2*cos(A*degree));
        var point_uy = point_ay - (Ds/2*sin(A*degree));
        var vec_u = vector(point_ux, point_uy);
        
        var point_bx = point_ax - W;
        var point_by = point_ay -V;
        var vec_b = vector(point_bx, point_by);
        
        // Draws the circle segment that makes up the top of the tooth
        // Pos. and Neg. versions are made to give mirror appearance
        skArc(sketch1, "arc1_neg", {
                "start" : (vec_b + vector(0, F/2)) * inch,                                  //90 deg from origin of point b, relative to +x-axis
                "mid" : (vec_b + vector(F/2*cos(45*degree), F/2*sin(45*degree))) * inch,    //45 deg from origin of point b
                "end" : (vec_b + vector(F/2, 0)) * inch                                     //0 deg from origin of point b
        });                                                                                 //Circle segment of 1st quadrant with origin as point b
        skArc(sketch1, "arc1_pos", {
                "start" : (vector(-point_bx, point_by) + vector(0, F/2)) * inch,                                    //origin is offset to the positive x-direction
                "mid" : (vector(-point_bx, point_by) + vector(-F/2*cos(45*degree), F/2*sin(45*degree))) * inch,     //Circle segment of 2nd quadrant with new origin
                "end" : (vector(-point_bx, point_by) + vector(F/2, 0)) * inch
        });
        
        // Sketchs the bottom half of circle_a which is where the chain will sit
        // Circle segment includes 3rd and 4th quadrants with point a as the origin
        skArc(sketch1, "arc2", {
                "start" : (vec_a + vector(-Ds/2, 0)) * inch,
                "mid" : (vec_a + vector(0, -Ds/2)) * inch,
                "end" : (vec_a + vector(Ds/2, 0)) * inch
        });
        
        // Sketchs the connection line from the point tangent to circle_a to tangent on circle_E
        // Using the the circle segment of the 1st qraduant with the origin set as point b
        // 2 Sketchs are made to give mirror appearance with the positive just been offset properly
        skArc(sketch1, "arc3_neg", {
                "start" : (vec_c + vector(-E/2, 0)) * inch,
                "mid" : vec_y  * inch,
                "end" : (vec_c + vector(-E/2*cos(45*degree), -E/2*sin(45*degree)))  * inch
        });
        skArc(sketch1, "arc3_pos", {
                "start" : (vector(-point_cx, point_cy) + vector(E/2, 0)) * inch,
                "mid" : vector(-point_yx, point_yy)  * inch,
                "end" : (vector(-point_cx, point_cy) + vector(E/2*cos(45*degree), -E/2*sin(45*degree)))  * inch
        });
        
        // Sketchs the last line segment that origins from the origin to the top of the tooth
        adjust_y = (PD/2 + E/2);
        adjust_x = adjust_y * tan(180/N*degree);
        skLineSegment(sketch1, "line_last_neg", {
                "start" : vector(0, 0) * inch,
                "end" : vector(-adjust_x, adjust_y) * inch
        });
        skLineSegment(sketch1, "line_last_pos", {
                "start" : vector(0, 0) * inch,
                "end" : vector(adjust_x, adjust_y) * inch
        });
        
        // Construction line and circles made for referrencing purposes only....
        skLineSegment(sketch1, "linetest", {
                "start" : vector(0, 0) * inch,
                "end" : vec_b * inch,
                "construction" : true
        });
        
        skCircle(sketch1, "circle_F_neg", {
                "center" : vec_b * inch,
                "radius" : F/2 * inch,
                "construction" : true
        });
        skCircle(sketch1, "circle_E_pos", {
                "center" : vector(-point_bx, point_by) * inch,
                "radius" : F/2 * inch,
                "construction" : true
        });
        skCircle(sketch1, "circle_E_neg", {
                "center" : vector(-point_cx, point_cy) * inch,
                "radius" : E/2 * inch,
                "construction" : true
        });
        skCircle(sketch1, "circle_Ds", {
                "center" : vec_a * inch,
                "radius" : Ds/2 * inch,
                "construction" : true
        });
        skSolve(sketch1);
        
        // Combination of solid lines and curves drawn should form a selectable face
        // Pulling the entity created by our sketch to a variable called Face
        // We can then use it to extrude to as a part which we can use a circular pattern on later
        
        var FACE = qCreatedBy(id + "sketch1", EntityType.FACE);

        extrude(context, id + "extrude1", {
                "entities" : FACE,
                "endBound" : BoundingType.BLIND,
                "depth" : definition.Depth,
                "operationType" : NewBodyOperationType.NEW
        });
        
        // Creates a spline line that is normal to our top plane in the +z direction
        opFitSpline(context, id + "fitSpline1", {
                "points" : [
                    point_o,
                    (point_o/inch + plane1.normal) * inch,
                ]
        });
        
        // Use the spline to create the variable axis which we will use as our axis of rotation
        var axis = qCreatedBy(id + "fitSpline1", EntityType.EDGE);
        // debug(context, qCreatedBy(id + "extrude1", EntityType.BODY));
        
        // Circular Pattern copies our single part around in a circle N times to create our gears
        // Make sure you merge all the patters with operationType....
        circularPattern(context, id + "circularPattern1", {
                "patternType" : PatternType.PART,
                "entities" : qCreatedBy(id + "extrude1", EntityType.BODY),
                "axis" : axis,
                "angle" : 360/N * degree,
                "instanceCount" : N,
                "operationType" : NewBodyOperationType.ADD
        });
        
        // This last part just draws the hole of the shalf to the removed from the center
    
        if (definition.BoreDiameter.value > 0)
        {
            var sketch2 = newSketchOnPlane(context, id + "sketch2", {
                "sketchPlane" : plane1 
            });
            skCircle(sketch2, "bore_hole", {
                    "center" : vector(0, 0) * inch,
                    "radius" : Bore_D/2
            });    
            skSolve(sketch2);
            extrude(context, id + "bore_extrude", {
                "entities" : qSketchRegion(id + "sketch2"),
                "endBound" : BoundingType.THROUGH_ALL,
                "depth" : Bore_D,
                "operationType" : NewBodyOperationType.REMOVE
        });
        }
    });
