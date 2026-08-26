FeatureScript 1576;
import(path : "onshape/std/common.fs", version : "1576.0");
import(path : "onshape/std/table.fs", version : "1576.0");
icon::import(path : "c64a48b07cd880c1a3a30dba", version : "8df855b48af83e8b0b588185");
image::import(path : "4f8bba4c22f54e218425c23b", version : "635231531c26e7a5ea7028a6");

// KO: Ready for review

annotation { "Feature Type Name" : "Belt", "Feature Name Template" : "Belt (#length)", "Icon" : icon::BLOB_DATA,
             "Feature Type Description" : "Create a belt around pulley faces.<br>" ~
                             "The belt may wrap around either side of pulley faces, but the belt motion must lie in a single plane.",
             "Description Image" : image::BLOB_DATA }
export const beltFeature = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        // Define the parameters of the feature type
        annotation { "Name" : "Pulley Faces", "Filter" : (GeometryType.CYLINDER || GeometryType.TORUS) && EntityType.FACE, "UIHint" : UIHint.ALLOW_QUERY_ORDER,
                    "Description" : "Cylindrical faces (or revolved arcs) of the pulleys the belt will wrap around" }
        definition.cylinders is Query;
        annotation { "Name" : "Flipped pulley faces", "Filter" : (GeometryType.CYLINDER || GeometryType.TORUS) && EntityType.FACE,
                    "Description" : "Faces (from the list above) which need the belt to wrap around the other side" }
        definition.flippedFaces is Query;
        annotation { "Name" : "Flipped pulley parts", "Filter" : EntityType.BODY, "UIHint" : UIHint.ALWAYS_HIDDEN }
        definition.flippedParts is Query; // Legacy field, ignored if flippedFaces is non-empty
        annotation { "Name" : "Automatic Ordering", "Default" : false,
                    "Description" : "If unchecked, belt is routed through pulleys in order of \"Flipped pulley faces\" selections" }
        definition.autoOrdering is boolean;
        annotation { "Name" : "Mid-plane", "Filter" : (GeometryType.PLANE && EntityType.FACE) || BodyType.MATE_CONNECTOR, "MaxNumberOfPicks" : 1 }
        definition.midPlane is Query;
        annotation { "Name" : "Width" }
        isLength(definition.width, LENGTH_BOUNDS);
        annotation { "Name" : "Thickness" }
        isLength(definition.thickness, SHELL_OFFSET_BOUNDS);
    }
    {
        var faces = evaluateQuery(context, definition.cylinders);
        if (size(faces) < 2)
        {
            throw regenError("Need at least two pulleys");
        }
        definition.beltPlane = findBeltPlane(context, definition);
        // All the axes MUST be parallel
        var cylinders = generateCylinders(context, definition, faces);
        var sketchId = id + "sketch";
        var extrudeId = id + "extrude";
        createBeltPathSketch(context, definition, cylinders, sketchId);
        var toCleanup = [];
        try
        {
            var length = extrudePathSketch(context, definition, sketchId, extrudeId);
            setFeatureComputedParameter(context, id, { "name" : "length", "value" : length });
            toCleanup = append(toCleanup, extrudeId);
            thickenExtrudedSurface(context, definition, extrudeId, id + "thicken");
            toCleanup = append(toCleanup, sketchId);
            var attribute = makeBeltAttribute(length, definition.width, definition.thickness);
            setAttribute(context, {
                "entities" : qCreatedBy(id + "thicken", EntityType.BODY),
                "attribute" : attribute,
                "name" : "beltAttribute",
            });
        }
        catch
        {
            debug(context, qCreatedBy(sketchId, EntityType.BODY)->qBodyType(BodyType.WIRE));
            throw regenError("Could not create solid belt from belt path (possibly self-intersecting path)");
        }
        if (size(toCleanup) > 0)
        {
            cleanupWorkBodies(context, toCleanup, id + "deleteBodies");
        }
    });

function findBeltPlane(context is Context, definition is map) returns Plane
{
    var beltPlane = undefined;
    var faces = evaluateQuery(context, qGeometry(definition.cylinders, GeometryType.TORUS));
    for (var face in faces)
    {
        var surface = evSurfaceDefinition(context, {
                "face" : face
            });
        const torusPlane = plane(surface.coordSystem);
        if (beltPlane != undefined)
        {
            if (!coplanarPlanes(torusPlane, beltPlane))
            {
                throw regenError("Pulley midplanes must be consistent", ["cylinders"]);
            }
        }
        else
        {
            beltPlane = torusPlane;
        }
    }
    const midPlaneQ = evaluateQuery(context, definition.midPlane);
    if (size(midPlaneQ) > 1 || (size(midPlaneQ) == 0 && beltPlane == undefined))
    {
        throw regenError("Need one and only one mid-plane");
    }
    else if (size(midPlaneQ) == 1)
    {
        const midPlane = evPlane(context, {
                    "face" : midPlaneQ[0]
                });
        if (beltPlane == undefined)
        {
            beltPlane = midPlane;
        }
        else if (!coplanarPlanes(beltPlane, midPlane))
        {
            throw regenError("Pulley midplanes must be consistent", ["cylinders", "midPlane"]);
        }
    }
    // Now finally we need to ensure the origin is the projection of the part studio origin, for legacy reasons
    return plane(project(beltPlane, vector(0, 0, 0) * meter), beltPlane.normal, beltPlane.x);
}

function getNextCylinder(cylinders is array, index is number) returns map
{
    return cylinders[(index + 1) % size(cylinders)];
}

function getPreviousCylinder(cylinders is array, index is number) returns map
{
    return cylinders[(index + size(cylinders) - 1) % size(cylinders)];
}

function getProfilePoints(definition is map, cylinders is array, index is number) returns map
{
    // positive chirality: if line direction is d then B = (R-r)/d and A = sqrt(1 - ((R-r)/d)^2) and points are c1 + R(Bd + A(dT)), c2 + r(Bd + A(dT))
    // negative chirality: if line direction is d then B = (R+r)/d and A = sqrt(1 - ((R+r)/d)^2) and points are c1 + R(Bd + A(dT)), c2 + r(Bd + A(dT))
    var cylinder1 = cylinders[index];
    var cylinder2 = getNextCylinder(cylinders, index);
    var radius1 = cylinder1.radius;
    var radius2 = cylinder2.radius;
    var center1 = cylinder1.sketchLocation;
    var center2 = cylinder2.sketchLocation;
    var delta = normalize(center2 - center1);
    var cross = vector(-delta[1], delta[0]);
    if (!cylinder2.chirality)
    {
        radius2 = -radius2;
    }
    if (!cylinder1.chirality)
    {
        radius1 = -radius1;
    }
    var alpha = (radius1 - radius2) / norm(center2 - center1);
    var beta = sqrt(1 - alpha * alpha);
    var point1 = center1 + (alpha * delta + beta * cross) * radius1;
    var point2 = center2 + (alpha * delta + beta * cross) * radius2;
    return { "point1" : point1, "point2" : point2, "radius1" : abs(radius1), "center1" : center1 };
}

/**
 * From the definition generate the list of cylinders to wrap around. Returns and array of surface definitions (typed results of evSurfaceDefinition) plus the following feilds:
 *
 * `radius` is replaced with radius of the ideal centerline of the belt
 *
 * `angle` is the angular location of this axis around the centroid of all axes (with an arbitrary start point)
 *
 * `chirality` is `true` if the belt should wrap around the "outside" of this axis (futher from the centroid)
 */
function generateCylinders(context is Context, definition is map, faces is array) returns array
{
    var planeDef = definition.beltPlane;
    var planeBasis = coordSystem(planeDef.origin, planeDef.x, planeDef.normal);
    const axis = planeBasis.zAxis;
    var mm = millimeter;
    var centroid = vector(0, 0) * mm;
    var cylinders = [];
    var toSketch = fromWorld(planeBasis);
    var zeroLocation = toSketch * (vector(0, 0, 0) * mm);
    for (var face in faces)
    {
        var surface = evSurfaceDefinition(context, {
                "face" : face
            });
        var cylinderAxis;
        if (surface is Torus)
        {
            cylinderAxis = surface.coordSystem.zAxis;
            // Get the extreme in the coord system x direction. Determine convexity of the roller at that based on distance from axis
            const testPoint = surface.coordSystem.origin + (surface.radius + surface.minorRadius) * surface.coordSystem.xAxis;
            const distance = evDistance(context, {
                        "side0" : face,
                        "side1" : testPoint
                    });
            const torusPoint = distance.sides[0].point;
            // IB Review: I'm confused about this calculation.  We project a point on the x-axis onto the trimmed face,
            //            and now we project that back onto the x axis.  I don't see how this determines convexity if the face is trimmed
            // KO comment: Not convexity exactly, but it does differentiate the cases in the "convex and concave" part studio. I don't think we care about concave pulley with more than half the inside of a torus (or a convex pulley which doesn't contain the part we'll stick the belt on) so I left this logic
            const extent = abs(dot(torusPoint - surface.coordSystem.origin, surface.coordSystem.xAxis));
            if (extent > surface.radius)
            {
                // In the concave case we will have the belt graze the pulley
                surface.radius = surface.radius + surface.minorRadius + definition.thickness * 0.5;
            }
            else
            {
                // In the convex case we will have the edges of the belt graze the pulley so we need to calculate the additional offset
                const shortened = surface.minorRadius * surface.minorRadius - (definition.width * definition.width * 0.25);
                if (shortened < 0 * meter * meter)
                {
                    throw regenError("Radius of curvature of roller is too small for the specified belt width", ["cylinders", "width"]);
                }
                surface.radius = surface.radius + definition.thickness * 0.5 - sqrt(shortened);
            }
        }
        else if (surface is Cylinder)
        {
            cylinderAxis = surface.coordSystem.zAxis;
            surface.radius = surface.radius + definition.thickness * 0.5;
        }
        else
        {
            throw regenError("Selection was not a cylinder");
        }
        if (!parallelVectors(axis, cylinderAxis))
        {
            throw regenError("All the cylinders need to be oriented in the same direction");
        }
        surface.sketchLocation = toSketch * (surface.coordSystem.origin - planeBasis.origin) - (zeroLocation * 2);
        surface.sketchLocation = vector(surface.sketchLocation[0], surface.sketchLocation[1]);
        if (isQueryEmpty(context, definition.flippedFaces))
        {
            surface.flipChirality = !isQueryEmpty(context, qIntersection(qOwnerBody(face), definition.flippedParts));
        }
        else
        {
            surface.flipChirality = !isQueryEmpty(context, qIntersection(face, definition.flippedFaces));
        }
        centroid += surface.sketchLocation;
        cylinders = append(cylinders, surface);
    }
    centroid /= size(faces);
    var temp = [];
    for (var cylinder in cylinders)
    {
        cylinder.angle = atan2(cylinder.sketchLocation[0] - centroid[0], cylinder.sketchLocation[1] - centroid[1]);
        temp = append(temp, cylinder);
    }
    if (definition.autoOrdering)
    {
        cylinders = tolerantSort(temp, 1e-3 * degree, function(cylinder)
                {
                    return cylinder.angle;
                });
    }
    else
    {
        cylinders = temp;
    }
    // Now that the cylinders are sorted determine the side of the path to go on
    var classified = [];
    if (size(cylinders) > 2)
    {
        for (var index = 0; index < size(cylinders); index += 1)
        {
            var cylinder = cylinders[index];
            var previous = getPreviousCylinder(cylinders, index);
            var next = getNextCylinder(cylinders, index);
            var vec1 = normalize(cylinder.sketchLocation - previous.sketchLocation);
            var vec2 = normalize(next.sketchLocation - cylinder.sketchLocation);
            cylinder.chirality = (vec1[0] * vec2[1] - vec1[1] * vec2[0]) < 0 != cylinder.flipChirality;
            classified = append(classified, cylinder);
        }
    }
    else
    {
        for (var index = 0; index < size(cylinders); index += 1)
        {
            var cylinder = cylinders[index];
            cylinder.chirality = true;
            classified = append(classified, cylinder);
        }
    }
    return classified;
}

function createBeltPathSketch(context is Context, definition is map, cylinders is array, sketchId is Id)
{
    var sketch = newSketchOnPlane(context, sketchId, { "sketchPlane" : definition.beltPlane });
    var profilePointArray = [];
    for (var index = 0; index < size(cylinders); index += 1)
    {
        var profilePoints = getProfilePoints(definition, cylinders, index);
        profilePointArray = append(profilePointArray, profilePoints);
        skLineSegment(sketch, "line" ~ index, { "start" : profilePoints.point1, "end" : profilePoints.point2 });
    }
    for (var index = 0; index < size(cylinders); index += 1)
    {
        var previousIndex = (index - 1) % size(cylinders);
        addArc(sketch, "arc" ~ index,
                [profilePointArray[previousIndex].point1, profilePointArray[previousIndex].point2,
                    profilePointArray[index].point1, profilePointArray[index].point2],
                profilePointArray[index].center1, profilePointArray[index].radius1,
                cylinders[index].chirality);
    }
    skSolve(sketch);
}

function addArc(sketch is Sketch, arcId is string, fourPoints is array, center is Vector, radius is ValueWithUnits, chirality is boolean)
precondition
{
    size(fourPoints) == 4;
}
{
    // fourPoints is an array of points [start of "in" line, end of "in" line, start of "out" line, end of "out" line]
    var midVec = ((fourPoints[1] + fourPoints[2]) * 0.5) - center;
    if (tolerantEquals(norm(midVec), 0 * millimeter))
    {
        var crossVec = fourPoints[2] - fourPoints[1];
        midVec = vector(-crossVec[1], crossVec[0]);
    }
    else
    {
        // If the in and out line lead to a 'bend' of greater than 180 degrees then we want to flip the midVec
        var inVec = normalize(fourPoints[1] - fourPoints[0]);
        var outVec = normalize(fourPoints[3] - fourPoints[2]);
        var crossProduct = inVec[0] * outVec[1] - inVec[1] * outVec[0];
        if (crossProduct < 0 != chirality)
        {
            midVec *= -1;
        }
    }
    var mid = normalize(midVec) * radius + center;
    var start;
    var end;
    if (chirality)
    {
        start = fourPoints[2];
        end = fourPoints[1];
    }
    else
    {
        start = fourPoints[1];
        end = fourPoints[2];
    }
    skArc(sketch, arcId, {
                "start" : start,
                "mid" : mid,
                "end" : end
            });
}

function extrudePathSketch(context is Context, definition is map, sketchId is Id, extrudeId is Id) returns ValueWithUnits
{
    var entities = qConstructionFilter(qBodyType(qCreatedBy(sketchId, EntityType.EDGE), BodyType.WIRE), ConstructionObject.NO);
    opExtrude(context, extrudeId, {
                "entities" : entities,
                "direction" : evOwnerSketchPlane(context, { "entity" : entities }).normal,
                "startBound" : BoundingType.BLIND,
                "startDepth" : definition.width / 2,
                "endBound" : BoundingType.BLIND,
                "endDepth" : definition.width / 2
            });
    return evLength(context, { "entities" : entities });
}

function thickenExtrudedSurface(context is Context, definition is map, extrudeId is Id, thickenId is Id)
{
    opThicken(context, thickenId, {
                "entities" : qCreatedBy(extrudeId, EntityType.FACE),
                "thickness1" : definition.thickness * 0.5,
                "thickness2" : definition.thickness * 0.5
            });
}

function cleanupWorkBodies(context is Context, featureIds is array, deleteId is Id)
{
    var queryArray = mapArray(featureIds, function(featureId)
    {
        return qCreatedBy(featureId, EntityType.BODY);
    });
    opDeleteBodies(context, deleteId, { "entities" : qUnion(queryArray) });
}

export type BeltAttribute typecheck canBeBeltAttribute;
export predicate canBeBeltAttribute(value)
{
    value is map;
    isLength(value.length, LENGTH_BOUNDS);
    isLength(value.width, LENGTH_BOUNDS);
    isLength(value.thickness, LENGTH_BOUNDS);
}

const tableTolerance = 0.001 * millimeter;
function makeBeltAttribute(length is ValueWithUnits, width is ValueWithUnits, thickness is ValueWithUnits) returns BeltAttribute
{
    return {
        // TODO: Instead use tolerant comparision, only considering relevant inputs
        "length" : round(length, tableTolerance),
        "width" : round(width, tableTolerance),
        "thickness" : round(thickness, tableTolerance),
    } as BeltAttribute;
}

annotation { "Table Type Name" : "Belts", "Icon" : icon::BLOB_DATA }
export const beltTable = defineTable(function(context is Context, definition is map) returns Table
    precondition
    {
    }
    {
        var columnDefinitions = [
            tableColumnDefinition("quantity", "Qty."),
            tableColumnDefinition("length", "Length"),
            tableColumnDefinition("width", "Width"),
            tableColumnDefinition("thickness", "Thickness"),
        ];

        var uniqueBelts = {};
        var partsWithBeltAttributes = qHasAttribute("beltAttribute");
        for (var part in evaluateQuery(context, partsWithBeltAttributes))
        {
            const beltAttribute = getAttribute(context, {
                    "entity" : part,
                    "name" : "beltAttribute",
            });
            if (uniqueBelts[beltAttribute] == undefined)
            {
                uniqueBelts[beltAttribute] = [ part ];
                continue;
            }
            uniqueBelts[beltAttribute] = append(uniqueBelts[beltAttribute], part);
        }

        var rows = [];
        
        for (var beltEntry in uniqueBelts)
        {
            const parts = beltEntry.value;
            var beltData = beltEntry.key;
            beltData.quantity = size(beltEntry.value);
            rows = append(rows, tableRow(beltData, qUnion(parts)));
        }

        return table("Belts", columnDefinitions, rows);
    });
