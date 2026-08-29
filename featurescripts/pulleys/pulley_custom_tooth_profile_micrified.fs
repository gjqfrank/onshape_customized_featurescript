FeatureScript 3008;
import(path : "onshape/std/common.fs", version : "3008.0");
IconNamespace::import(path : "92a5091924a774e635dbdeec", version : "31bf56fe297a210d422a55cd");

/*\
 *******************************************************************************
 *                           Type Definition: Path2D                           *
 *******************************************************************************
\*/

type Segment2D typecheck canBeSegment2D;
predicate canBeSegment2D(value)
{
    value is map;
    size(value) == 2;
    is2dPoint(value["start"]);
    is2dPoint(value["end"]);
}

function segment2D(start is Vector, end is Vector) returns Segment2D
{
    return { "start" : start, "end" : end } as Segment2D;
}

type Segment3D typecheck canBeSegment3D;
predicate canBeSegment3D(value)
{
    value is map;
    size(value) == 2;
    is3dLengthVector(value["start"]);
    is3dLengthVector(value["end"]);
}

function segment3D(start is Vector, end is Vector) returns Segment3D
{
    return { "start" : start, "end" : end } as Segment3D;
}

type Path2D typecheck canBePath2D;
predicate canBePath2D(value)
{
    value is array;
    for (var v in value)
    {
        is2dPoint(v);
    }
}

function path2D(start is Vector, end is Vector, segments is array) returns Path2D
precondition
{
    size(filterSegments(start, segments)) == 1;
    size(filterSegments(end, segments)) == 1;
    for (var segment in segments)
    {
        is2dPoint(segment.start);
        is2dPoint(segment.end);
    }
}
{
    return prependPath2D(start, segments2DToPath2D(start, segments));
}

function repeatPath2D(seed is array, count is number) returns Path2D
precondition
{
    count > 0;
}
{
    var path is array = [];
    for (var i = 0; i < count; i += 1)
    {
        for (var v in seed)
        {
            path = append(path, v);
        }
    }
    return path as Path2D;
}

function prependPath2D(first is Vector, path is Path2D) returns Path2D
{
    var prepended is array = [first];
    for (var v in path)
    {
        prepended = append(prepended, v);
    }
    return prepended as Path2D;
}

function segments2DToPath2D(v is Vector, segments is array) returns Path2D
{
    var next is array = filterSegments(v, segments);
    if (size(next) > 1)
    {
        throw "Path may not have branches!";
    }
    else if (size(next) == 1)
    {
        const index is number = next[0];
        const start is Vector = segments[index].start;
        const end is Vector = segments[index].end;
        if (tolerantEquals(v, start) && tolerantEquals(v, end))
        {
            throw "Path may not loop!";
        }
        if (tolerantEquals(v, start))
        {
            return prependPath2D(end, segments2DToPath2D(end, delete(index, segments)));
        }
        else
        {
            return prependPath2D(start, segments2DToPath2D(start, delete(index, segments)));
        }
    }
    return [] as Path2D;
}

function filterSegments(v is Vector, segments is array) returns array
{
    var indexes is array = [];
    for (var index, segment in segments)
    {
        if (tolerantEquals(v, segment.start) || tolerantEquals(v, segment.end))
        {
            indexes = append(indexes, index);
        }
    }
    return indexes;
}

function delete(indexToRemove is number, from is array) returns array
{
    var filtered is array = [];
    for (var index, value in from)
    {
        if (index != indexToRemove)
        {
            filtered = append(filtered, value);
        }
    }
    return filtered;
}

function edgesToSegments3D(context is Context, edgesQuery is Query) returns array
{
    var segments3D is array = [];
    for (var edgeQuery in evaluateQuery(context, qEntityFilter(edgesQuery, EntityType.EDGE)))
    {
        segments3D = append(segments3D, segment3D(evVertexPoint(context, {
                "vertex" : qEdgeVertex(edgeQuery, true)
        }), evVertexPoint(context, {
                "vertex" : qEdgeVertex(edgeQuery, false)
        })));
    }
    return segments3D;
}

function toSegments2D(segments3D is array, plane is Plane) returns array
{
    var segments2D is array = [];
    for (var segment3D in segments3D)
    {
        const start is Vector = worldToPlane(plane, segment3D.start);
        const end is Vector = worldToPlane(plane, segment3D.end);
        segments2D = append(segments2D, segment2D(start, end));
    }
    return segments2D;
}

/*\
 *******************************************************************************
 *                                Enumerations                                 *
 *******************************************************************************
\*/

export enum AxisEnum
{
    annotation { "Name" : "X", "Icon" : Icon.ALONG_X }
    X,
    annotation { "Name" : "Y", "Icon" : Icon.ALONG_Y }
    Y,
    annotation { "Name" : "Z", "Icon" : Icon.ALONG_Z }
    Z
}

const AxisToDirection is map = {
    AxisEnum.X : X_DIRECTION,
    AxisEnum.Y : Y_DIRECTION,
    AxisEnum.Z : Z_DIRECTION
};

export enum InterpolationModeEnum
{
    annotation { "Name" : "Linear (edge)" }
    LINEAR,
    annotation { "Name" : "Curved (arc)" }
    ARC
}

/*\
 *******************************************************************************
 *                             Feature Definition                              *
 *******************************************************************************
\*/

annotation
{
        "Feature Type Name" : "Pulley",
        "Feature Type Description" : "Create a pulley with a custom sketch profile",
        "Filter Selector" : ["pulley"],
        "UIHint" : "NO_PREVIEW_PROVIDED",
        "Icon" : IconNamespace::BLOB_DATA
}
export const pulley = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Group Name" : "Profile", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Sketch", "Filter" : (EntityType.EDGE && SketchObject.YES)}
            definition.geometry is Query;
            
            annotation { "Name" : "Pitch Line", "Filter" : (EntityType.EDGE && SketchObject.YES && ConstructionObject.YES), "MaxNumberOfPicks" : 1}
            definition.pitchLine is Query;
            
            annotation { "Name" : "Path Start Vertex", "Filter" : (EntityType.VERTEX && SketchObject.YES && ConstructionObject.NO), "MaxNumberOfPicks" : 1}
            definition.pathStartVertex is Query;
            
            annotation { "Name" : "Path End Vertex", "Filter" : (EntityType.VERTEX && SketchObject.YES && ConstructionObject.NO), "MaxNumberOfPicks" : 1}
            definition.pathEndVertex is Query;
        }
        
        annotation { "Group Name" : "Adjustments", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Filter collinear points" }
            definition.filterCollinearPoints is boolean;
            
            annotation { "Name" : "Interpolation Mode" }
            definition.interpolationMode is InterpolationModeEnum;
        }
        
        annotation { "Group Name" : "Specification", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Tooth Count" }
            isInteger(definition.toothCount, { (unitless) : [1, 12, 1024] } as IntegerBoundSpec);
            
            annotation { "Name" : "Width" }
            isLength(definition.width, NONNEGATIVE_LENGTH_BOUNDS);
        }
        
        annotation { "Group Name" : "Placement", "Collapsed By Default" : false }
        {
                  annotation { "Name" : "Mate Connector", "Filter" : BodyType.MATE_CONNECTOR, "MaxNumberOfPicks" : 1 }
                  definition.mateConnector is Query;
                  
                  annotation { "Name" : "Normal Axis" }
                  definition.mateNormalAxis is AxisEnum;
                  
                  annotation { "Name" : "Opposite Direction", "UIHint" : "OPPOSITE_DIRECTION" }
                  definition.mateOppositeDirection is boolean;
        }
    }
    {
        // Create bounding box
        var boundingBox is Box3d = evBox3d(context, {
                "topology" : definition.geometry,
                "tight" : true
        });
        //debug(context, boundingBox, DebugColor.BLUE);
        
        // Determine the plane in which the pitch profile lies
        var pitchPlane is Plane = evPlanarEdges(context, {
                "edges" : qEntityFilter(definition.geometry, EntityType.EDGE)
        });
        
        // Create a sketch plane with origin at the bounding box corner
        const sketchPlane is Plane = plane(boundingBox.minCorner, pitchPlane.normal, pitchPlane.x);
        
        // Create a sketch on the plane
        const sketchId = id + "sketch";
        const sketch = newSketchOnPlane(context, sketchId, {
                "sketchPlane" : sketchPlane
        });
        
        // Validate the pitch line lies within the sketch plane
        if (isQueryEmpty(context, qCoincidesWithPlane(definition.pitchLine, sketchPlane)))
        {
            reportFeatureWarning(context, id, "Pitch line should lie within the sketch plane");
            addDebugEntities(context, definition.pitchLine, DebugColor.RED);
        }
        
        // Locate major axis as that which is parallel to the pitch line
        var majorAxis is Vector = cross(sketchPlane.x, sketchPlane.normal);
        if (!isQueryEmpty(context, qParallelEdges(definition.pitchLine, sketchPlane.x)))
        {
            majorAxis = sketchPlane.x;
        }
        else if (!isQueryEmpty(context, qParallelEdges(definition.pitchLine, majorAxis)))
        {
            majorAxis = vector(abs(majorAxis[0]), abs(majorAxis[1]), abs(majorAxis[2]));
        }
        else
        {
            reportFeatureWarning(context, id, "Pitch line should be parallel to one of the sketch plane axes");
            addDebugEntities(context, definition.pitchLine, DebugColor.RED);  
        }
        debug(context, definition.pitchLine, DebugColor.BLUE);
        
        // Extract the height of the pitch line relative to origin
        const pitchLineOffset is ValueWithUnits = worldToPlane(sketchPlane, evVertexPoint(context, {
                "vertex" : qEdgeVertex(definition.pitchLine, false)
        }))[1];
        println("The pitch line is at: " ~ toString(pitchLineOffset));
        
        // Calculate the pitch-circle radius using:
        // tooth-pitch: width of the bounding box
        // pitch-circle radius: product of teeth-count and tooth-pitch over 2PI
        // Calculate inner radius from the major axis of the bounding box, and sketch inner circle
        const pitchWidth is ValueWithUnits = dot((boundingBox.maxCorner - boundingBox.minCorner), majorAxis);
        const pitchCircleRadius is ValueWithUnits = ((definition.toothCount * pitchWidth) / (2 * PI));
        const outerCircleId = "circle.pitch";
        skCircle(sketch, outerCircleId, {
                "center" : zeroVector(2) * meter,
                "radius" : pitchCircleRadius,
                "construction" : true
        });
        
        // Extract profile edges
        const profileEdgesQuery is Query = qConstructionFilter(definition.geometry, ConstructionObject.NO)->qEntityFilter(EntityType.EDGE);
        debug(context, profileEdgesQuery, DebugColor.GREEN);
        
        // Calculate path
        var path is Path2D = [] as Path2D;
        try
        {
            const pathStartVertex is Vector = worldToPlane(sketchPlane, evVertexPoint(context, {
                    "vertex" : definition.pathStartVertex
            }));
            const pathEndVertex is Vector = worldToPlane(sketchPlane, evVertexPoint(context, {
                    "vertex" : definition.pathEndVertex
            }));
            var pathSegments2D is array = edgesToSegments3D(context, profileEdgesQuery)->toSegments2D(sketchPlane);
            path = path2D(pathStartVertex, pathEndVertex, pathSegments2D);
        }
        catch
        {
            reportFeatureWarning(context, id, "Profile path must be an unbroken and non-branching sequence of non-construction edges");
        }
        
        // Generate the pulley by converting the path to a loop, then translating the line segment vertices 
        var loop is Path2D = repeatPath2D(delete(0, path), definition.toothCount);
        for (var i = 0; i < definition.toothCount; i += 1)
        {
            const translateVector is Vector = vector(i * pitchWidth, pitchCircleRadius - pitchLineOffset);
            for (var j = 0; j < (size(path) - 1); j += 1)
            {
                loop[i * (size(path) -1) + j] += translateVector;
            }
        }
        
        // Filter collinear points
        if (definition.filterCollinearPoints)
        {
            loop = filterCollinearPoints2D(loop);
        }
        
        // Extend the path by one extra point, to allow rational midpoint calculation
        loop = append(loop, vector(definition.toothCount * pitchWidth, 0 * meter) + loop[0]);
        
        // Generate new geometry (loop to just before duplicate last element)
        for (var i = 0; i < (size(loop) - 1); i += 1)
        {
            const start is Vector = loop[i];
            const end is Vector = loop[(i+1)%size(loop)];
            const midpoint is Vector = (start + end) / 2.0;
            
            if (definition.interpolationMode == InterpolationModeEnum.ARC)
            {
                skArc(sketch, "arc" ~ i, {
                        "start" : rotateClockwise2D(start + vector(-start[0], 0 * meter), (start[0] / pitchCircleRadius) * radian),
                        "mid" : rotateClockwise2D(midpoint + vector(-midpoint[0], 0 * meter), (midpoint[0] / pitchCircleRadius) * radian),
                        "end" : rotateClockwise2D(end + vector(-end[0], 0 * meter), (end[0] / pitchCircleRadius) * radian)
                });
            }
            else
            {
                skLineSegment(sketch, "line" ~ i, {
                    "start" : rotateClockwise2D(start + vector(-start[0], 0 * meter), (start[0] / pitchCircleRadius) * radian),
                    "end"   : rotateClockwise2D(end + vector(-end[0], 0 * meter), (end[0] / pitchCircleRadius) * radian)
                });           
            }
        }
        
        // Solve sketch
        skSolve(sketch);
        
        // Obtain a reference to the sketch region, then extrude it.
        const extrudeRegion = qSketchRegion(sketchId);
        const extrudeId = id + "extrude";
        opExtrude(context, extrudeId, {
                "entities" : extrudeRegion,
                "direction" : sketchPlane.normal,
                "endBound" : BoundingType.BLIND,
                "endDepth" : definition.width
        });
        
        // Fetch the mate connector
        const mateCoordSystem is CoordSystem = evMateConnector(context, {
                "mateConnector" : definition.mateConnector
        });
        
        // Transform extrusion to mate connector
        const mateOppositeDirection is number = definition.mateOppositeDirection ? -1 : 1;
        opTransform(context, id + "transform", {
                "bodies" : qCreatedBy(extrudeId, EntityType.BODY),
                "transform" : rotationAround(line(mateCoordSystem.origin, AxisToDirection[definition.mateNormalAxis]), 
                mateOppositeDirection * (PI / 2) * radian) * toWorld(mateCoordSystem)
        });
        
        // Clean up
        opDeleteBodies(context, id + "deleteBodies", {
                "entities" : qCreatedBy(sketchId)
        });
        
    });
        
function collinearPoints2D(p1 is Vector, p2 is Vector, p3 is Vector) returns boolean
precondition
{
    is2dPoint(p1);
    is2dPoint(p2);
    is2dPoint(p3);
}
{
    return tolerantEquals((p2[1] - p1[1]) * (p3[0] - p2[0]), (p3[1] - p2[1]) * (p2[0] - p1[0]));
}

function filterCollinearPoints2D(path is Path2D) returns Path2D
{
    const n = size(path);
    var filtered is array = [];
    for (var i = 0; i < n; i += 1)
    {
        if (!collinearPoints2D(path[(i-1)%n], path[i], path[(i+1)%n]))
        {
            filtered = append(filtered, path[i]);
        }
    }
    return filtered as Path2D;
}

function rotateClockwise2D(point is Vector, angle is ValueWithUnits) returns Vector
{
    const x1 is number = point[0] / meter;
    const x2 is number = point[1] / meter;
    return vector(
            cos(angle) * x1 + sin(angle) * x2,
            -sin(angle) * x1 + cos(angle) * x2
    ) * meter;
}
