FeatureScript 1174;
import(path : "onshape/std/geometry.fs", version : "1174.0");

annotation { "Feature Type Name" : "Sprocket"}
export const Sprocket = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Select Sprocket Plane", "Filter" : GeometryType.PLANE, "MaxNumberOfPicks" : 1 }
        definition.plane is Query;
            
        annotation { "Name" : "Select Sprocket Center", "Filter" : EntityType.VERTEX, "MaxNumberOfPicks" : 1 }
        definition.centerPoint is Query;

        annotation { "Name" : "Chain Number", "UIHint" : "SHOW_LABEL"}
        definition.Chain is Chain;
        
        annotation { "Name" : "Choose number of teeth" }
        definition.choose is boolean;
        
        if (definition.choose)
        {
            annotation { "Name" : "Teeth" } 
            isInteger(definition.teeth, POSITIVE_COUNT_BOUNDS); 
        }
        else
        {
            annotation { "Name" : "Sprocket Diameter" }
            isReal(definition.SD, POSITIVE_REAL_BOUNDS);
        }
        
        annotation { "Name" : "Bore Diameter" }
        isReal(definition.HD, POSITIVE_REAL_BOUNDS);
        
        annotation { "Name" : "Laser Cuter Diameter" }
        definition.error is boolean;
        
        if (definition.error)
        {
            annotation { "Name" : "Diameter" }
            isReal(definition.LD, POSITIVE_REAL_BOUNDS);
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
        print("Chain Values : ");
        debug(context, ChainValues[definition.Chain].rollerD);
        
        definition.plane = evPlane(context, {
                "face" : definition.plane
        });
        print("Plane selected : ");
        debug(context, definition.plane);
        
        definition.centerPoint = evVertexPoint(context, {
                "vertex" : definition.centerPoint
        });
        print("Point selected : ");
        debug(context, definition.centerPoint);
        
        var RD = ChainValues[definition.Chain].rollerD;
        var P = ChainValues[definition.Chain].pitch;
        var RW = ChainValues[definition.Chain].rollerW;
        var HD = definition.HD;
        var SD = definition.SD;
        
        if (isPointOnPlane(definition.centerPoint, definition.plane) == false)
        {
            throw regenError("Point doesn't belong to the Plane selected", ["centerPoint"]);
        }
        
        if (HD > (0.7 * (SD - RD)))
        {
            throw regenError("Hole diameter is too large", ["HD"]);
        }
        
        RD = RD * 1.00075 + 0.003;
        
        var alpha = asin(P * 0.5 / SD);
        // debug(context, alpha/degree);
        const teeth = ceil(360 / (2 * alpha) * degree);
        // println("teeth = ");
        // debug(context, teeth);
        alpha = 180 * degree/ teeth;
        // println("alpha = ");
        // debug(context, (alpha/degree));
        
        var location = definition.centerPoint;
        var sketchPlane = plane(location, definition.plane.normal);
        const sprocketSketch = newSketchOnPlane(context, id + "sprocketSketch", { "sketchPlane" : sketchPlane });
        const center = worldToPlane(sketchPlane, location);
        
        // opFitSpline(context, id + "axis", { "points" : [ vector( 0,  0,  0) * inch, vector( 0,  -1, 0) * inch ]});
        // const axis = qCreatedBy(id + "axis", EntityType.EDGE);
        
        // debug(context, axis);
        
        // skLineSegment(sprocketSketch, "line1", {
        //     "start" : vector(0, 0) * inch,
        //     "end" : vector(-(definition.CP / 2), (definition.SD * cos(alpha)) ) * inch
        // });
        
        // skLineSegment(sprocketSketch, "line2", {
        //     "start" : vector(0, 0) * inch,
        //     "end" : vector((definition.CP / 2), (definition.SD * cos(alpha))) * inch
        // });
        
        // skLineSegment(sprocketSketch, "line3", {
        //         "start" : vector(0 - (definition.CP / 2), (definition.SD * cos(alpha))) * inch,
        //         "end" : vector(0 + (definition.CP / 2), (definition.SD * cos(alpha))) * inch
        // });
        
        // const center1 = vector(0, (definition.SD * cos(alpha)) + definition.RD * 0.4 / 2, 0) * inch;
        // const majorAxis1 = vector(0, 1, 0) * inch;
        // const minorRadius1 = vector((definition.CP - definition.RD) * 0.5, 0, 0) * inch;
        // const majorRadius1 = vector((definition.CP - definition.RD) * 0.7, 0, 0) * inch;
        // const startParameter1 = vector(-0.25, 0, 0) * inch;
        // const endParameter1 = vector(0.25, 0, 0) * inch;
        
        // const center2 = vector(center[0]/inch - (definition.CP * 0.5), definition.RD / 2 + center[1]/inch + ( definition.SD * cos(alpha)), 0) * inch;
        // const majorAxis2 = vector(1, 0, 0) * inch;
        // const minorRadius2 = vector(definition.RD * 0.5, 0, 0) * inch;
        // const majorRadius2 = vector(definition.RD * 0.5, 0, 0) * inch;
        // const startParameter2 = vector(-0.4, 0, 0) * inch;
        // const endParameter2 = vector(0, 0, 0) * inch;
        
        // const center3 = vector((center[0]/inch - (definition.CP * 0.5)) * (-1), definition.RD / 2 + center[1]/inch + ( definition.SD * cos(alpha)), 0) * inch;
        // const majorAxis3 = vector(1, 0, 0) * inch;
        // const minorRadius3 = vector(definition.RD * 0.5, 0, 0) * inch;
        // const majorRadius3 = vector(definition.RD * 0.5, 0, 0) * inch;
        // const startParameter3 = vector(0.5, 0, 0) * inch;
        // const endParameter3 = vector(-0.1, 0, 0) * inch;
        
        // const axis1 = line(vector(0, 0, 0) *inch, vector(0, 0, 1));
        // const full = PI * 2 * radian;
        
        // for (var i = 0; i < 1; i += 1)
        // {
        //     const elid1 = "eli1" ~ i;
        //     const elid2 = "eli2" ~ i;
        //     const elid3 = "eli3" ~ i;
            
        //     const rotation = rotationAround(axis1, (i  / teeth) * full);
            
        //     const center1_3d = rotation * center1;
        //     const majorAxis1_3d = rotation * majorAxis1;
        //     const minorRadius1_3d = rotation * minorRadius1;
        //     const majorRadius1_3d = rotation * majorRadius1;
        //     const startParameter1_3d = rotation * startParameter1;
        //     const endParameter1_3d = rotation * endParameter1;
            
        //     const center2_3d = rotation * center2;
        //     const majorAxis2_3d = rotation * majorAxis2;
        //     const minorRadius2_3d = rotation * minorRadius2;
        //     const majorRadius2_3d = rotation * majorRadius2;
        //     const startParameter2_3d = rotation * startParameter2;
        //     const endParameter2_3d = rotation * endParameter2;
            
        //     const center3_3d = rotation * center3;
        //     const majorAxis3_3d = rotation * majorAxis3;
        //     const minorRadius3_3d = rotation * minorRadius3;
        //     const majorRadius3_3d = rotation * majorRadius3;
        //     const startParameter3_3d = rotation * startParameter3;
        //     const endParameter3_3d =  rotation * endParameter3;
            
        //     const center1_2d = vector(center1_3d[0], center1_3d[1]);
        //     const majorAxis1_2d = vector(majorAxis1_3d[0], majorAxis1_3d[1]);
        //     const minorRadius1_2d = vector(minorRadius1_3d[0], minorRadius1_3d[1]);
        //     const majorRadius1_2d = vector(majorRadius1_3d[0], majorRadius1_3d[1]);
        //     const startParameter1_2d = vector(startParameter1_3d[0], startParameter1_3d[1]);
        //     const endParameter1_2d = vector(endParameter1_3d[0], endParameter1_3d[1]);
            
        //     const center2_2d = vector(center2_3d[0], center2_3d[1]);
        //     const majorAxis2_2d = vector(majorAxis2_3d[0], majorAxis2_3d[1]);
        //     const minorRadius2_2d = vector(minorRadius2_3d[0], minorRadius2_3d[1]);
        //     const majorRadius2_2d = vector(majorRadius2_3d[0], majorRadius2_3d[1]);
        //     const startParameter2_2d = vector(startParameter2_3d[0], startParameter2_3d[1]);
        //     const endParameter2_2d = vector(endParameter2_3d[0], endParameter2_3d[1]);
            
        //     const center3_2d = vector(center3_3d[0], center3_3d[1]);
        //     const majorAxis3_2d = vector(majorAxis3_3d[0], majorAxis3_3d[1]);
        //     const minorRadius3_2d = vector(minorRadius3_3d[0], minorRadius3_3d[1]);
        //     const majorRadius3_2d = vector(majorRadius3_3d[0], majorRadius3_3d[1]);
        //     const startParameter3_2d = vector(startParameter3_3d[0], startParameter3_3d[1]);
        //     const endParameter3_2d = vector(endParameter3_3d[0], endParameter3_3d[1]);
            
        //     // debug(context, rotation);
        //     // debug(context, center1_2d);
        //     // debug(context, minorRadius1_2d);
        //     // debug(context, startParameter1_2d[0]);
            
        //     skEllipticalArc(sprocketSketch, elid1, {
        //         "center" : center1_2d,
        //         "majorAxis" : majorAxis1_2d / inch,
        //         "minorRadius" : minorRadius1_2d[0],
        //         "majorRadius" : majorRadius1_2d[0],
        //         "startParameter" : startParameter1_2d[0] / inch,
        //         "endParameter" : endParameter1_2d[0] / inch
        //     });
            
            
        //     skEllipticalArc(sprocketSketch, elid2, {
        //         "center" : center2_2d,
        //         "majorAxis" : vector(majorAxis2_2d[0], majorAxis2_2d[1]),
        //         "minorRadius" : minorRadius2_2d[0],
        //         "majorRadius" : majorRadius2_2d[0],
        //         "startParameter" : startParameter2_2d[0] / inch,
        //         "endParameter" : endParameter2_2d[0] / inch
        //     });
            
        //     skEllipticalArc(sprocketSketch, elid3, {
        //         "center" : center3_2d,
        //         "majorAxis" : vector(majorAxis3_2d[0], majorAxis3_2d[1]),
        //         "minorRadius" : minorRadius3_2d[0],
        //         "majorRadius" : majorRadius3_2d[0],
        //         "startParameter" : startParameter3_2d[0] / inch,
        //         "endParameter" : endParameter3_2d[0] / inch
        //     });
        // }
        
        // const center1 = vector(0, SD * cos(alpha)) * inch;
        // const majorAxis1 = vector(1, 0);
        // const minorRadius1 = (P - RD) * 0.7 * inch;
        // const majorRadius1 = (P - RD) * 0.49 * inch;
        // const startParameter1 = 0;
        // const endParameter1 = 0.5;
        
        // skEllipticalArc(sprocketSketch, "ellipticalArc1", {
        //         "center" : center1,
        //         "majorAxis" : majorAxis1,
        //         "minorRadius" : minorRadius1,
        //         "majorRadius" : majorRadius1,
        //         "startParameter" : startParameter1,
        //         "endParameter" : endParameter1,
        //         "construction" : true
        // });
        
        // const center2 = vector(0 - P * 0.495, SD * cos(alpha)) * inch;
        // const majorAxis2 = vector(1, 0);
        // const minorRadius2 = (RD * 0.5) * inch;
        // const majorRadius2 = (RD * 0.5) * inch;
        // const startParameter2 = 0.45;
        // const endParameter2 = 0.05;
        
        // skEllipticalArc(sprocketSketch, "ellipticalArc2", {
        //         "center" : center2,
        //         "majorAxis" : majorAxis2,
        //         "minorRadius" : minorRadius2,
        //         "majorRadius" : majorRadius2,
        //         "startParameter" : startParameter2,
        //         "endParameter" : endParameter2,
        //         "construction" : true
        // });
        
        // const center3 = vector(0 + P * 0.495, SD * cos(alpha)) * inch;
        // const majorAxis3 = vector(1, 0);
        // const minorRadius3 = RD * 0.5 * inch;
        // const majorRadius3 = RD * 0.5 * inch;
        // const startParameter3 = 0.45;
        // const endParameter3 = 0.05;
        
        // skEllipticalArc(sprocketSketch, "ellipticalArc3", {
        //         "center" : center3,
        //         "majorAxis" : majorAxis3,
        //         "minorRadius" : minorRadius3,
        //         "majorRadius" : majorRadius3,
        //         "startParameter" : startParameter3,
        //         "endParameter" : endParameter3,
        //         "construction" : true
        // });
        
        // skArc(sprocketSketch, "arc1", {
        //         "start" : vector(1, 0) * inch,
        //         "mid" : vector(0, 0) * inch,
        //         "end" : vector(-1, 0) * inch
        // });
        
        // skCircle(sprocketSketch, "circle1", {
        //     "center" : vector(center[0]/inch - (definition.CP * 0.5), center[1]/inch + ( definition.SD * cos(alpha))) * inch,
        //     "radius" : definition.RD * 0.5 * inch
        // });
        
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

        const axis2 = line(vector(0, 0, 0) * inch, vector(0, 0, 1));
        const full1 = 360 * degree;

        // Loop through the required number of instances, transform the points and create the geometry
         for (var i = 0; i < teeth; i += 1)
        {
            const arcId1 = "arc1" ~ i;
            const arcId2 = "arc2" ~ i;
            const arcId3 = "arc3" ~ i;
            
            const lineId1 = "line1" ~ i;            
            const lineId2 = "line2" ~ i;

            const rotation = rotationAround(axis2, (i / teeth) * full1);
            
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
        
        // const ellipseCenter = vector(0, definition.SD - (definition.CP *0.5 * tan(alpha))) - vector(center[0]/inch, center[1]/inch);
        
        // skEllipse(sprocketSketch, "ellipse1", {
        //         "center" : ellipseCenter,
        //         "majorRadius" : (definition.CP - definition.RD) * 0.5 * inch,
        //         "minorRadius" : (definition.CP - definition.RD) * 0.75 * inch
        // });
        
        // skRegularPolygon(sprocketSketch, "polyId", {
        //         "center" : center,
        //         "firstVertex" : vector(center[0]/inch - (definition.CP * 0.5), center[1]/inch + ( definition.SD * cos(alpha))) * inch,
        //         "sides" : teeth
        // });

        
        // const rollerSketch = newSketchOnPlane(context, id + "rollerSketch", { "sketchPlane" : sketchPlane });
        
        // skCircle(rollerSketch, "circle1", {
        //     "center" : vector(center[0]/inch - (definition.CP * 0.5), center[1]/inch + ( definition.SD * cos(alpha))) * inch,
        //     "radius" : definition.RD * 0.5 * inch
        // });
        
        // skCircle(rollerSketch, "circle2", {
        //     "center" : vector(center[0]/inch + (definition.CP * 0.5), center[1]/inch + ( definition.SD * cos(alpha))) * inch,
        //     "radius" : definition.RD * 0.5 * inch
        // });
        
        // skSolve(rollerSketch);
        
        // var location2 = vector(0, 0, 0) * inch;
        // var teethSketchPlane = plane(location2, vector(0, -1, 0), vector(1, 0, 0));
        // const teethSketch = newSketchOnPlane(context, id + "teethSketch", { "sketchPlane" : teethSketchPlane });
        // const teethCenter = vector(0, definition.SD * cos(alpha));
        
        // skEllipticalArc(teethSketch, "ellipticalArc1", {
        //     "center" : teethCenter * inch,
        //     "majorAxis" : vector(0, 1),
        //     "minorRadius" : (definition.CP - definition.RD) * 0.5 * inch,
        //     "majorRadius" : (definition.CP - definition.RD) * 0.75 * inch,
        //     "startParameter" : -0.25,
        //     "endParameter" : 0.25
        // });
        
        // skEllipse(teethSketch, "ellipse1", {
        //         "center" : teethCenter * inch,
        //         "majorRadius" : (definition.CP - definition.RD) * 0.5 * inch,
        //         "minorRadius" : (definition.CP - definition.RD) * 0.75 * inch
        // });
        
        // skEllipticalArc(teethSketch, "ellipticalArc1", {
        //     "center" : vector(0, (definition.SD * cos(alpha)) + definition.RD / 2) * inch,
        //     "majorAxis" : vector(0, 1),
        //     "minorRadius" : (definition.CP - definition.RD) * 0.5 * inch,
        //     "majorRadius" : (definition.CP - definition.RD) * 0.70 * inch,
        //     "startParameter" : -0.25,
        //     "endParameter" : 0.25
        // });
        
        // skEllipticalArc(teethSketch, "ellipticalArc2", {
        //     "center" : vector(center[0]/inch - (definition.CP * 0.5), definition.RD / 2 + center[1]/inch + ( definition.SD * cos(alpha))) * inch,
        //     "majorAxis" : vector(1, 0),
        //     "minorRadius" : definition.RD * 0.5 * inch,
        //     "majorRadius" : definition.RD * 0.5 * inch,
        //     "startParameter" : -0.25,
        //     "endParameter" : 0
        // });
        
        // skEllipticalArc(teethSketch, "ellipticalArc3", {
        //     "center" : vector((center[0]/inch - (definition.CP * 0.5)) * (-1), definition.RD / 2 + center[1]/inch + ( definition.SD * cos(alpha))) * inch,
        //     "majorAxis" : vector(1, 0),
        //     "minorRadius" : definition.RD * 0.5 * inch,
        //     "majorRadius" : definition.RD * 0.5 * inch,
        //     "startParameter" : 0.5,
        //     "endParameter" : -0.25
        // });  
        
        // skLineSegment(teethSketch, "line4", {
        //         "start" : vector(0 - (definition.CP / 2), (definition.SD * cos(alpha))) * inch,
        //         "end" : vector(0 + (definition.CP / 2), (definition.SD * cos(alpha))) * inch
        // });
        
        
        // skSolve(teethSketch);
        
        var holeSketchPlane = plane(location, definition.plane.normal);
        const holeSketch = newSketchOnPlane(context, id + "holeSketch", { "sketchPlane" : holeSketchPlane });
        const holeCenter = vector(0, 0);
        
        skCircle(holeSketch, "circle1", {
                "center" : vector(0, 0) * inch,
                "radius" : HD * inch
        });
        
        skSolve(holeSketch);
        
        // var faces = evaluateQuery(context, qEverything(EntityType.FACE));
        // debug(context, faces);
        
        // circularPattern(context, id + "teethSketch", {
        //         "patternType" : PatternType.PART,
        //         "entities" : qCreatedBy(id + "teethSketch", EntityType.BODY),
        //         "axis" : axis,
        //         "angle" : 360 * degree,
        //         "instanceCount" : teeth
        // });
        
        // annotation { "Name" : "My Query", "Filter" : EntityType.FACE, "MaxNumberOfPicks" : 1 }
        // definition.myQuery is Query;
        
        // var test = dummyQuery(operationId, EntityType.VERTEX);
        
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
                 "entities" : qSketchRegion(id + "holeSketch"),
                 "endBound" : BoundingType.BLIND,
                 "depth" : RW * 0.9 * inch,
                 "operationType" : NewBodyOperationType.REMOVE,
                 "oppositeDirection" : definition.flipExtrude
            });
            
            // extrude(context, id + "extrude1", {
            //         "entities" : qSketchRegion(id + "sprocketSketch"),
            //         "endBound" : BoundingType.SYMMETRIC,
            //         "depth" : definition.RD * inch
            // }); 
            
            // circularPattern(context, id + "circularPattern1", {
            //         "patternType" : PatternType.FACE,
            //         "entities" : qCreatedBy(id + "extrude1", EntityType.EDGE),
            //         "axis" : axis,
            //         "angle" : 2 * alpha,
            //         "instanceCount" : teeth,
            //         // "operationType" : NewBodyOperationType.ADD
            // });
            
            // extrude(context, id + "extrude2", {
            //         "entities" : qSketchRegion(id + "teethSketch"),
            //         "endBound" : BoundingType.SYMMETRIC,
            //         "depth" : definition.RW * 0.9 * inch,
            //         "operationType" : NewBodyOperationType.NEW
            // });
            
            // circularPattern(context, id + "circularPattern2", {
            //         "patternType" : PatternType.PART,
            //         "entities" : qCreatedBy(id + "extrude2", EntityType.BODY),
            //         "axis" : axis,
            //         "angle" : 2 * alpha,
            //         "instanceCount" : teeth,
            //         "operationType" : NewBodyOperationType.ADD
            // });
            
            // var q1 = qEntityFilter(qEverything(EntityType.BODY), EntityType.BODY);
            
            // debug(context, evaluateQuery(context, q1));
            
            // opBoolean(context, id + "boolean1", {
            //         "tools" : q1,
            //         "operationType" : BooleanOperationType.UNION
            // });
            
            // extrude(context, id + "extrude3", {
            //         "entities" : qSketchRegion(id + "teethSketch"),
            //         "endBound" : BoundingType.SYMMETRIC,
            //         "depth" : definition.RW * 0.9 * inch,
            //         "operationType" : NewBodyOperationType.ADD
            // });
                        
            // circularPattern(context, id + "circularPattern3", {
            //         "patternType" : PatternType.PART,
            //         "entities" : qCreatedBy(id + "extrude3", EntityType.BODY),
            //         "axis" : sketchPlane.normal,
            //         "angle" : 360 * degree,
            //         "instanceCount" : teeth
            // });
                        
            // var edges = qParallelEdges(qCreatedBy(id + "extrude1"));
            
            // var pointY = vector(0, -1, 0);
            
            // qEverything(EntityType.VERTEX);
            
            // debug(context, pointY);
            
            // const axis = evVertexPoint(context, {
            //         "vertex" : pointY
            // });
            
            // debug(context, axis);
        }
        
                // skSolv(sprocketSketch);
        
        // skLineSegment(sprocketSketch, "line1", {
        //         "start" : vector(center[0]/inch, center[1]/inch) * inch,
        //         "end" : vector(0, -1) * inch,
        //         "construction" : true
        // });
        

        // linearPattern(context, id + "linearPattern1", {
        //         "patternType" : PatternType.FACE,
        //         "entities" : qCreatedBy(id + "circle1", EntityType.FACE),
        //         "directionOne" : qCreatedBy(newId() + "Right", EntityType.FACE),
        //         "distance" : 1.0 * inch,
        //         "instanceCount" : 2
        // });
        
        // debug(context, );
        
    });
    
export enum Chain
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

export const ChainValues = 
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
