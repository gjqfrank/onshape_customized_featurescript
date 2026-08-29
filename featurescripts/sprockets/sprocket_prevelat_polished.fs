FeatureScript 1174;
import(path : "onshape/std/geometry.fs", version : "1174.0");
tmb::import(path : "c282eef0096883cedd4cdbfe/4d079bc058cdf77146cad154/d12187017453b6827da34a77", version : "89dc1b2985fe7cee54363500");

annotation { "Feature Type Name" : "Sprocket (inch)", "Icon" : tmb::BLOB_DATA}
export const Sprocket = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Select Sprocket Plane", "Filter" : GeometryType.PLANE, "MaxNumberOfPicks" : 1 }
        definition.plane is Query;
            
        annotation { "Name" : "Select Sprocket Center", "Filter" : EntityType.VERTEX, "MaxNumberOfPicks" : 1 }
        definition.centerPoint is Query;

        annotation { "Name" : "Chain Number", "UIHint" : "SHOW_LABEL"}
        definition.Chain is Chain;
        
        annotation { "Name" : "Sprocket Radius" }
        isReal(definition.SR, MY_SR_BOUNDS);
        
        annotation { "Name" : "Bore Radius"}
        isReal(definition.BR, MY_BR_BOUNDS);
        
        annotation { "Name" : "Add Laser Radius"}
        definition.error is boolean;
        
        if (definition.error)
        {
            annotation { "Name" : "Radius" }
            isReal(definition.LR, MY_LR_BOUNDS);
        }        
        
        annotation { "Name" : "3D Model" }
        definition.extrude is boolean;
        
        if (definition.extrude)
        {
            annotation { "Name" : "Extrude direction", "UIHint" : "OPPOSITE_DIRECTION" }
            definition.flipExtrude is boolean;
        }
    }
           
    {
        // Evaluate plane and point selected
        definition.plane = evPlane(context, { "face" : definition.plane });
        definition.centerPoint = evVertexPoint(context, { "vertex" : definition.centerPoint });
        if (isPointOnPlane(definition.centerPoint, definition.plane) == false)
        {
            throw regenError("Point doesn't belong to the Plane selected", ["centerPoint"]);
        }
        
        // Evaluate variables
        debug(context, definition.LR);
        var RD = ChainValues[definition.Chain].rollerD * 1.00075 + 0.003 - definition.LR; // add error margin
        var P = ChainValues[definition.Chain].pitch;
        var RW = ChainValues[definition.Chain].rollerW;
        var BD = 2 * definition.BR;
        var SD = 2 * definition.SR;
        var alpha = asin(P * 0.5 / SD);
        var teeth = ceil(360 / (2 * alpha) * degree);
        print("Number of teeth:"); println(teeth); /*show teeth count in Part Studio notices*/
        alpha = 180 * degree/ teeth;
        
        // Create teeth sketch plane
        var location = definition.centerPoint;
        var sketchPlane = plane(location, definition.plane.normal);
        const sprocketSketch = newSketchOnPlane(context, id + "sprocketSketch", { "sketchPlane" : sketchPlane });
        
        // Draw 1st tooth profile 
        const start1 = vector((0 - (P * 0.495) - (RD * 0.5)) * 1.01, SD * cos(alpha), 0) * inch;
        const mid1 = vector(0 - (P * 0.495), (SD * cos(alpha) - RD/2) * 0.999, 0) * inch;
        const end1 = vector((0 - ((P - RD) / 2)) * 0.95, SD * cos(alpha), 0) * inch;
        
        const start2 = vector(0 - ((P - RD) * 0.4), SD + ((P - RD) * 0.25), 0) * inch;
        const mid2 = vector(0, SD + ((P - RD) * 0.6), 0) * inch;
        const end2 = vector(((P - RD) * 0.4), SD + ((P - RD) * 0.25), 0) * inch;
        
        const start3 = vector((0 - ((P - RD) / 2)) * -0.95, SD * cos(alpha), 0) * inch;
        const mid3 = vector(P * 0.495, (SD * cos(alpha) - RD/2) * 0.999, 0) * inch;
        const end3 = vector((0 - (P * 0.495) - (RD * 0.5)) * -1.01, SD * cos(alpha), 0) * inch;
        
        const lstart1 = vector((0 - ((P - RD) / 2)) * 0.95, SD * cos(alpha), 0) * inch;
        const lend1 = vector(0 - ((P - RD) * 0.4), SD + ((P - RD) * 0.25), 0) * inch;
        
        const lstart2 = vector(((P - RD) * 0.4), SD + ((P - RD) * 0.25), 0) * inch;
        const lend2 = vector((0 - ((P - RD) / 2)) * -0.95, SD * cos(alpha), 0) * inch;


        // Loop through the required number of teeth, transform the points and create profile
        const axis = line(vector(0, 0, 0) * inch, vector(0, 0, 1));
        const full = 360 * degree;
        
        for (var i = 0; i < teeth; i += 1)
        {
            const arcId1 = "arc1" ~ i;
            const arcId2 = "arc2" ~ i;
            const arcId3 = "arc3" ~ i;
            
            const lineId1 = "line1" ~ i;            
            const lineId2 = "line2" ~ i;

            const rotation = rotationAround(axis, (i / teeth) * full);
            
            const start3d1 = rotation * start1;
            const mid3d1 = rotation * mid1;
            const end3d1 = rotation * end1;
            
            const start3d2 = rotation * start2;
            const mid3d2 = rotation * mid2;
            const end3d2 = rotation * end2;

            const start3d3 = rotation * start3;
            const mid3d3 = rotation * mid3;
            const end3d3 = rotation * end3;
            
            const lstart3d1 = rotation * lstart1;
            const lend3d1 = rotation * lend1;
            
            const lstart3d2 = rotation * lstart2;
            const lend3d2 = rotation * lend2;

            const start2d1 = vector(start3d1[0], start3d1[1]);
            const mid2d1 = vector(mid3d1[0], mid3d1[1]);
            const end2d1 = vector(end3d1[0], end3d1[1]);
            
            const start2d2 = vector(start3d2[0], start3d2[1]);
            const mid2d2 = vector(mid3d2[0], mid3d2[1]);
            const end2d2 = vector(end3d2[0], end3d2[1]);
            
            const start2d3 = vector(start3d3[0], start3d3[1]);
            const mid2d3 = vector(mid3d3[0], mid3d3[1]);
            const end2d3 = vector(end3d3[0], end3d3[1]);
            
            const lstart2d1 = vector(lstart3d1[0], lstart3d1[1]);
            const lend2d1 = vector(lend3d1[0], lend3d1[1]);
            
            const lstart2d2 = vector(lstart3d2[0], lstart3d2[1]);
            const lend2d2 = vector(lend3d2[0], lend3d2[1]);

            skArc(sprocketSketch, arcId1, {
                        "start" : start2d1,
                        "mid" : mid2d1,
                        "end" : end2d1
                    });
                    
            skArc(sprocketSketch, arcId2, {
                        "start" : start2d2,
                        "mid" : mid2d2,
                        "end" : end2d2
                    });
                    
            skArc(sprocketSketch, arcId3, {
                        "start" : start2d3,
                        "mid" : mid2d3,
                        "end" : end2d3
                    });
                    
            skLineSegment(sprocketSketch, lineId1, {
                        "start" : lstart2d1,
                        "end" : lend2d1 
                    });
                    
            skLineSegment(sprocketSketch, lineId2, {
                        "start" : lstart2d2,
                        "end" : lend2d2 
                    });
        }
        
        skSolve(sprocketSketch);
 
        // Draw bore profile
        var boreSketchPlane = plane(location, definition.plane.normal);
        const boreSketch = newSketchOnPlane(context, id + "boreSketch", { "sketchPlane" : boreSketchPlane });
        
        skCircle(boreSketch, "circle1", {
                "center" : vector(0, 0) * inch,
                "radius" : BD / 2 * inch
        });
        
        skSolve(boreSketch);
        
        // Build 3D model
        if (definition.extrude)
        {
            
            extrude(context, id + "extrude1", {
                 "entities" : qSketchRegion(id + "sprocketSketch"),
                 "endBound" : BoundingType.BLIND,
                 "depth" : RW * 0.9 * inch,
                 "operationType" : NewBodyOperationType.NEW,
                 "oppositeDirection" : definition.flipExtrude
            });
                   
            extrude(context, id + "extrude2", {
                 "entities" : qSketchRegion(id + "boreSketch"),
                 "endBound" : BoundingType.BLIND,
                 "depth" : RW * 0.9 * inch,
                 "defaultScope" : false,
                 "booleanScope" : qCreatedBy(id + "extrude1", EntityType.BODY),
                 "operationType" : NewBodyOperationType.REMOVE,
                 "oppositeDirection" : definition.flipExtrude
            });
        }
    });

export enum Chain // UI dropdown tab
{
    annotation { "Name" : "#25" } n25,
    annotation { "Name" : "#35" } n35,
    annotation { "Name" : "#40" } n40,
    annotation { "Name" : "#41" } n41,
    annotation { "Name" : "#50" } n50,
    annotation { "Name" : "#60" } n60,
    annotation { "Name" : "#80" } n80,
    annotation { "Name" : "#100" } n100,
    annotation { "Name" : "#120" } n120,
    annotation { "Name" : "#140" } n140,
    annotation { "Name" : "#160" } n160,
    annotation { "Name" : "#180" } n180,
    annotation { "Name" : "#200" } n200,
    annotation { "Name" : "#240" } n240
}

export const ChainValues = // Standard chain values
{
  Chain.n25 : {pitch : 1/4, rollerW : 1/8, rollerD : 0.130},
  Chain.n35 : {pitch : 3/8, rollerW : 3/16, rollerD : 0.200},
  Chain.n40 : {pitch : 1/2, rollerW : 1/4, rollerD : 0.306},
  Chain.n41 : {pitch : 1/2, rollerW : 5/16, rollerD : 0.312},
  Chain.n50 : {pitch : 5/8, rollerW : 3/8, rollerD : 0.400},
  Chain.n60 : {pitch : 3/4, rollerW : 1/2, rollerD : 0.469},
  Chain.n80 : {pitch : 1, rollerW : 5/8, rollerD : 0.625},
  Chain.n100 : {pitch : 1 + 1/4, rollerW : 3/4, rollerD : 0.750},
  Chain.n120 : {pitch : 1 + 1/2, rollerW :1, rollerD : 0.875},
  Chain.n140 : {pitch : 1 + 3/4, rollerW : 1, rollerD : 1},
  Chain.n160 : {pitch : 2, rollerW : 1 + 1/4, rollerD : 1.125},
  Chain.n180 : {pitch : 2 + 1/4, rollerW : 1 + 13/32, rollerD : 1.406},
  Chain.n200 : {pitch : 2 + 1/2, rollerW : 1 + 1/2, rollerD : 1.562},
  Chain.n240 : {pitch : 3, rollerW : 1 + 7/8, rollerD : 1.875}
};

// UI limits
export const MY_LR_BOUNDS =
{
    (unitless) : [0, 0, 10]
}as RealBoundSpec;

export const MY_BR_BOUNDS =
{
    (unitless) : [1e-5, 0.125, 1e5]
}as RealBoundSpec;

export const MY_SR_BOUNDS =
{
    (unitless) : [1e-5, 1, 1e5]
}as RealBoundSpec;
