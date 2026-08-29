FeatureScript 2384;
import(path : "onshape/std/common.fs", version : "2384.0");
import(path : "onshape/std/chamfer.fs", version : "2384.0");
import(path : "8b8c46128a5dbc2594925f4a", version : "b5486481d7d62c8db15aa3a3");

export import(path : "484d2d590d4a2ab919981b0e", version : "05fbcdf1bd02added3f41f54");
import(path : "6c65805103086c85362ee4b7", version : "23404bb0112cf9ce499110fb");
import(path : "b75434df23d86ba9542f761e", version : "9266d26ff57222400ed9a571");
import(path : "0103ad63394d7713fbf44448", version : "514d8101de7f3201e15c6456");
import(path : "0794d10863d10d98a88c2ab4", version : "79c7d92986385bcda86e65f5");

Pulley::import(path : "14228cd65b8beec25eef8b1d", version : "5e674b9e2e38ddf4e32c84f4");

annotation {
        "Feature Type Name" : "Robot pulley",
        "Manipulator Change Function" : "robotPulleyManipulatorChange",
        "Feature Type Description" : "Create GT2 and HTD pulleys." ~
        "<br>See also the Robot belt FeatureScript, which works directly with this feature." ~ CREDIT,
        "Icon" : RobotIcon::BLOB_DATA
    }
export const robotPulley = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        pulleyPredicate(definition);
    }
    {
        doRobotPulley(context, id, definition);
    });

function doRobotPulley(context is Context, id is Id, definition is map)
{
    var pulleyDefinitions;
    if (definition.creationMethod == CreationMethod.MANUAL)
    {
        const pointDistances = getPointDistances(definition);
        pulleyDefinitions = getManualPulleyDefinitions(context, definition, pointDistances);
        addManualManipulators(context, id, definition, pulleyDefinitions[0], pointDistances);
    }
    else
    {
        pulleyDefinitions = getBeltPulleyDefinitions(context, definition);
    }

    // Try to add manipulators as early as possible
    if (definition.addFlanges && definition.addText)
    {
        addTextPositionManipulator(context, id, definition, pulleyDefinitions[0]);
    }

    const profileOffset = getPulleyProfileOffset(definition);
    const pulleys = createPulleys(context, id + "pulley", pulleyDefinitions, profileOffset);

    if (definition.offsetProfile)
    {
        addProfileManipulator(context, id, definition, pulleyDefinitions[0].plane, pulleys[0]);
    }

    for (var i, pulleyDefinition in pulleyDefinitions)
    {
        setPulleyProperties(context, pulleyDefinition, pulleys[i]);
    }

    if (definition.addFlanges)
    {
        addFlanges(context, id + "flange", pulleyDefinitions, pulleys, definition.flangeSize, definition.unitSystem, profileOffset);
    }

    if (definition.addMateConnectors)
    {
        addMateConnectors(context, id + "mateConnector", pulleyDefinitions, pulleys, definition.unitSystem);
    }

    if (definition.addBore)
    {
        addBores(context, id + "bore", definition, pulleyDefinitions, pulleys);
    }

    // Add last to allow handling bore overlap error
    if (definition.addFlanges && definition.addText)
    {
        addText(context, id + "text", definition, pulleyDefinitions, pulleys);
    }
}

function addManualManipulators(context is Context, id is Id, definition is map, pulleyDefinition is PulleyDefinition, pointDistances is array)
{
    const plane = pulleyDefinition.plane;

    var startPlane = plane;
    startPlane.origin += startPlane.normal * getPointDistance(definition, pointDistances);
    addStartOffsetManipulator(context, id, definition, startPlane);

    const points = mapArray(pointDistances, function(distance)
        {
            return plane.origin + plane.normal * distance;
        });
    addPointManipulator(context, id, definition, points);
}

function addProfileManipulator(context is Context, id is Id, definition is map, plane is Plane, pulley is Query)
{
    const profileAxis = line(plane.origin, -plane.x);
    const pulleyFaces = qOwnedByBody(pulley, EntityType.FACE);
    const outsideFaces = pulleyFaces->qSubtraction(pulleyFaces->qParallelPlanes(plane));
    addProfileOffsetManipulator(context, id, PROFILE_OFFSET_MANIPULATOR, profileAxis, outsideFaces, definition.oppositeDirection);
}

/**
 * Computes the radius of a flange.
 */
function getFlangeRadius(definition is map, beltSize is BeltSize, teeth is number) returns ValueWithUnits
{
    const pulleyRadius = getPulleyRadius(getBeltPitch(beltSize), teeth) + getPulleyProfileOffset(definition);
    return pulleyRadius + getFlangeRadiusOffset(beltSize, definition.flangeSize, definition.unitSystem);
}

function getPulleyWidth(definition is map, beltSize is BeltSize, twoBelts is boolean) returns ValueWithUnits
{
    const extraWidth = twoBelts ? getTwoBeltPulleyExtraWidth(beltSize, definition.unitSystem) : 0 * meter;
    if (definition.addFlanges)
    {
        return getFlangedPulleyWidth(beltSize, definition.flangeSize, definition.unitSystem) + extraWidth;
    }
    return getPulleyTeethWidth(beltSize) + extraWidth;
}

function getPointDistances(definition is map) returns array
{
    const beltSize = getBeltSize(definition);
    const pulleyWidth = getPulleyWidth(definition, beltSize, definition.twoBelts);
    if (!definition.twoBelts)
    {
        return vector([0 * meter, pulleyWidth, -pulleyWidth]) / 2;
    }
    const extraWidth = getTwoBeltPulleyExtraWidth(beltSize, definition.unitSystem);
    return vector([extraWidth, pulleyWidth, -pulleyWidth, -extraWidth]) / 2;
}

/**
 * @type {{
 *      @field plane : A plane at the center of the pulley.
 *      @field width : The width of the pulley face.
 *      @field twoBelts : Whether or not the pulley is a two belt wide pulley.
 * }}
 */
type PulleyDefinition typecheck canBePulleyDefinition;

export predicate canBePulleyDefinition(value)
{
    value is map;
    value.identity is Query;
    value.plane is Plane;
    value.beltSize is BeltSize;
    value.teeth is number;
    value.width is ValueWithUnits;
    value.twoBelts is boolean;
}

function getManualPulleyDefinitions(context is Context, definition is map, pointDistances is array) returns array
{
    const locations = verifyNonemptyLocations(context, definition);
    const beltSize = getBeltSize(definition);
    return mapArray(locations, function(location)
        {
            const basePlane = evVertexCoordSystem(context, { "vertex" : location })->plane();
            var plane = applyStartOffset(context, definition, basePlane);
            plane = applyPointManipulator(definition, plane, pointDistances);

            var pulleyWidth = getPulleyTeethWidth(beltSize);
            if (definition.twoBelts)
            {
                const extraWidth = getTwoBeltPulleyExtraWidth(beltSize, definition.unitSystem);
                pulleyWidth += extraWidth;
            }

            return {
                        "identity" : location,
                        "plane" : plane,
                        "beltSize" : beltSize,
                        "teeth" : definition.pulleyTeeth,
                        "width" : pulleyWidth,
                        "twoBelts" : definition.twoBelts
                    } as PulleyDefinition;
        });
}

function getBeltPulleyDefinitions(context is Context, definition is map) returns array
{
    const beltSelections = verifyNonemptyQuery(context, definition, "beltSelections", "Select curved belt faces or belt mate connectors to use.");
    // Deduplicate belts by location to prevent duplicate pulley creation
    // Also handles case where belt teeth are added to selected face, resulting in duplicate pulleys
    var usedLocations = {};
    var pulleyDefinitions = [];
    for (var selection in beltSelections)
    {
        const attribute = getBeltPulleyFaceAttribute(context, selection);
            if (attribute == undefined)
            {
                throw regenError("Selected face is not a valid pulley face belonging to a robot belt.", ["beltSelections"], selection);
            }
            else if (attribute.pulleyType == PulleyType.IDLER)
            {
                throw regenError("Cannot add pulley to idler location.", ["beltSelections"], selection);
            }

            var pulleyPlane; // Avoid shadowing the plane function
            if (isMateConnector(context, selection))
            {
                pulleyPlane = evVertexCoordSystem(context, { "vertex" : selection })->plane();
            }
            else
            {
                pulleyPlane = evSurfaceDefinition(context, { "face" : selection }).coordSystem->plane();
                pulleyPlane.x = pulleyPlane->yAxis(); // Rotate plane to be consistent with belts

                const beltWidth = getBeltWidth(attribute.beltSize);
                pulleyPlane.origin += pulleyPlane.normal * beltWidth / 2;
            }

            if (usedLocations[pulleyPlane.origin] != undefined)
            {
                continue;
            }
            usedLocations[pulleyPlane.origin] = true;

            var pulleyWidth = getPulleyTeethWidth(attribute.beltSize);
            if (definition.twoBelts)
            {
                const extraWidth = getTwoBeltPulleyExtraWidth(attribute.beltSize, definition.unitSystem);
                pulleyWidth += extraWidth;
                pulleyPlane.origin += pulleyPlane.normal * (definition.flipTwoBeltSide ? -1 : 1) * extraWidth / 2;
            }

            const pulleyDefinition = {
                        "identity" : selection,
                        "plane" : pulleyPlane,
                        "teeth" : attribute.pulleyTeeth,
                        "beltSize" : attribute.beltSize,
                        "width" : pulleyWidth,
                        "twoBelts" : definition.twoBelts
                    } as PulleyDefinition;
            pulleyDefinitions = append(pulleyDefinitions, pulleyDefinition);
    }
    return pulleyDefinitions;
}

function getBeltPulleyFaceAttribute(context is Context, selection is Query)
{
    return getAttribute(context, {
                "entity" : isMateConnector(context, selection) ? selection->qOwnerBody() : selection,
                "name" : BELT_PULLEY_FACE_ATTRIBUTE
            });
}

function createPulleys(context is Context, id is Id, pulleyDefinitions is array, profileOffset is ValueWithUnits) returns array
{
    const instantiator = newInstantiator(id);

    var pulleys = [];
    for (var pulleyDefinition in pulleyDefinitions)
    {
        const configuration = {
                "beltType" : getBeltType(pulleyDefinition.beltSize),
                "width" : pulleyDefinition.width,
                "teeth" : pulleyDefinition.teeth,
                "profileOffset" : abs(profileOffset),
                "oppositeDirection" : profileOffset < 0
            };

        const pulley = addInstance(instantiator, Pulley::build, {
                    "identity" : pulleyDefinition.identity,
                    "transform" : pulleyDefinition.plane->coordSystem()->toWorld(),
                    "configuration" : configuration
                });
        pulleys = append(pulleys, pulley);
    }

    instantiate(context, instantiator);
    return pulleys;
}

function addMateConnectors(context is Context, id is Id, pulleyDefinitions is array, pulleys is array, unitSystem is UnitSystem)
{
    for (var i, pulleyDefinition in pulleyDefinitions)
    {
        const plane = pulleyDefinition.plane;
        const extraWidth = pulleyDefinition.twoBelts ? getTwoBeltPulleyExtraWidth(pulleyDefinition.beltSize, unitSystem) : 0 * meter;

        var firstCoordSystem = plane->coordSystem();
        firstCoordSystem.origin -= plane.normal * extraWidth / 2;

        const mateConnectorId = id + unstableIdComponent(i);
        setExternalDisambiguation(context, mateConnectorId, pulleyDefinition.identity);

        opMateConnector(context, mateConnectorId + "first", {
                    "coordSystem" : firstCoordSystem,
                    "owner" : pulleys[i]
                });

        if (pulleyDefinition.twoBelts)
        {
            var secondCoordSystem = plane->coordSystem();
            secondCoordSystem.origin += plane.normal * extraWidth / 2;

            opMateConnector(context, mateConnectorId + "second", {
                        "coordSystem" : secondCoordSystem,
                        "owner" : pulleys[i]
                    });
        }

        setAttribute(context, {
                    "entities" : qCreatedBy(mateConnectorId, EntityType.BODY)->qBodyType(BodyType.MATE_CONNECTOR),
                    "name" : PULLEY_ATTRIBUTE,
                    "attribute" : { "beltSize" : pulleyDefinition.beltSize, "pulleyTeeth" : pulleyDefinition.teeth } as PulleyAttribute
                });
    }
}


function addFlanges(context is Context, id is Id, pulleyDefinitions is array, pulleys is array, flangeSize is FlangeSize, unitSystem is UnitSystem, profileOffset is ValueWithUnits)
{
    for (var i, pulleyDefinition in pulleyDefinitions)
    {
        const beltSize = pulleyDefinition.beltSize;
        const pulleyPlane = pulleyDefinition.plane;
        const pulleyWidth = pulleyDefinition.width;

        const radius = getPulleyRadius(getBeltPitch(beltSize), pulleyDefinition.teeth) + profileOffset;
        const flangeRadiusOffset = getFlangeRadiusOffset(beltSize, flangeSize, unitSystem);
        const flangeWidth = getFlangeWidth(beltSize, flangeSize, unitSystem);

        const flangeId = id + unstableIdComponent(i);
        setExternalDisambiguation(context, flangeId, pulleyDefinition.identity);

        // Create a plane perpendicular to the pulley
        // origin is the center of the pulley face, x is away from the pulley
        const flangePlane = plane(pulleyPlane.origin + pulleyPlane.normal * pulleyWidth / 2, pulleyPlane.x, pulleyPlane.normal);
        const sketchId = flangeId + "sketch";
        const sketch = newSketchOnPlane(context, flangeId + "sketch", { "sketchPlane" : flangePlane });

        skLineSegment(sketch, "inside", {
                    "start" : zeroVector(2) * meter,
                    "end" : vector(0 * meter, radius)
                });

        skLineSegment(sketch, "outside", {
                    "start" : vector(flangeWidth, 0 * meter),
                    "end" : vector(flangeWidth, radius + flangeRadiusOffset)
                });

        skLineSegment(sketch, "bottom", {
                    "start" : zeroVector(2) * meter,
                    "end" : vector(flangeWidth, 0 * meter)
                });

        const topPoint = vector(flangeRadiusOffset, radius + flangeRadiusOffset);
        skLineSegment(sketch, "top", {
                    "start" : topPoint,
                    "end" : vector(flangeWidth, radius + flangeRadiusOffset)
                });
        skLineSegment(sketch, "chamfer", {
                    "start" : vector(0 * meter, radius),
                    "end" : topPoint
                });

        skSolve(sketch);

        opRevolve(context, flangeId + "revolve", {
                    "entities" : qCreatedBy(sketchId, EntityType.FACE),
                    "axis" : line(pulleyPlane.origin, pulleyPlane.normal),
                    "angleForward" : 0 * radian
                });

        opPattern(context, flangeId + "mirror", {
                    "entities" : qCreatedBy(flangeId + "revolve", EntityType.BODY),
                    "transforms" : [mirrorAcross(pulleyPlane)],
                    "instanceNames" : ["flangeCopy"]
                });

        opBoolean(context, flangeId + "boolean", {
                    "tools" : qUnion(pulleys[i], qCreatedBy(flangeId, EntityType.BODY)->qBodyType(BodyType.SOLID)),
                    "operationType" : BooleanOperationType.UNION
                });
    }

    cleanup(context, id + "deleteSketches", qCreatedBy(id, EntityType.BODY)->qSketchFilter(SketchObject.YES));
}


function setPulleyProperties(context is Context, pulleyDefinition is PulleyDefinition, pulley is Query)
{
    setProperty(context, {
                "entities" : pulley,
                "propertyType" : PropertyType.NAME,
                // 24T RT25 Pulley
                "value" : pulleyDefinition.teeth ~ "T " ~ getBeltType(pulleyDefinition.beltSize) ~ " Pulley"
            });

    setProperty(context, {
                "entities" : pulley,
                "propertyType" : PropertyType.MATERIAL,
                "value" : material("Onyx", 1.18 * gram / centimeter ^ 3)
            });

    setProperty(context, {
                "entities" : pulley,
                "propertyType" : PropertyType.APPEARANCE,
                "value" : BLACK
            });
}

function addBores(context is Context, id is Id, definition is map, pulleyDefinitions is array, pulleys is array)
{
    // We first generate the bores, then compute the manipulators before cutting the bores
    // This guarantees that we'll be able to find a suitable bore face to attach the manipulator to
    const boreResult = createBores(context, id + "bore", definition, pulleyDefinitions, pulleys);
    const bores = boreResult.bores;
    const firstBoreFaces = boreResult.firstBoreFaces;

    if (definition.boreType == BoreType.SPLINE && definition.offsetBoreProfile)
    {
        const plane = pulleyDefinitions[0].plane;
        const profileAxis = line(plane.origin, plane.x);
        addProfileOffsetManipulator(context, id, BORE_PROFILE_OFFSET_MANIPULATOR, profileAxis, firstBoreFaces, definition.boreOppositeDirection);
    }

    const insideEdges = startTracking(context, qNonCapEntity(id + "bore", EntityType.EDGE));

    try
    {
        opBoolean(context, id + "cutBore", {
                    "tools" : qUnion(bores),
                    "targets" : qUnion(pulleys),
                    "operationType" : BooleanOperationType.SUBTRACTION
                });
    }
    catch
    {
        addBoreDebugEntities(context, id, definition, pulleyDefinitions);
        throw regenError("Failed to add bore. Check input.", ["hexSize", "holeDiameter"]);
    }

    if (size(evaluateQuery(context, qUnion(pulleys))) != size(pulleys))
    {
        addBoreDebugEntities(context, id, definition, pulleyDefinitions);
        throw regenError("Failed to add bore. Check input.", ["hexSize", "holeDiameter"]);
    }

    if (definition.entranceChamfer)
    {
        const boreEdges = qCreatedBy(id + "cutBore", EntityType.EDGE)->qSubtraction(insideEdges);

        try
        {
            opChamfer(context, id + "entranceChamfer", {
                        "entities" : boreEdges,
                        "chamferType" : ChamferType.EQUAL_OFFSETS,
                        "width" : definition.chamferDistance
                    });
        }
        catch
        {
            addBoreDebugEntities(context, id, definition, pulleyDefinitions);
            throw regenError("Failed to chamfer bore. Check input.", ["hexSize", "holeDiameter", "chamferDistance"]);
        }
    }
}

function createBores(context is Context, id is Id, definition is map, pulleyDefinitions is array, pulleys is array) returns map
{
    const extrudeDistance = evBox3d(context, {
                    "topology" : qEverything(EntityType.BODY),
                    "tight" : false
                })->box3dDiagonalLength();

    var bores = [];
    for (var i, pulleyDefinition in pulleyDefinitions)
    {
        const boreId = id + unstableIdComponent(i);
        setExternalDisambiguation(context, boreId, pulleyDefinition.identity);
        const bore = createBore(context, boreId, definition, pulleyDefinition.plane, extrudeDistance);
        bores = append(bores, bore);
    }
    cleanup(context, id + "delete", qCreatedBy(id, EntityType.BODY)->qSketchFilter(SketchObject.YES));

    return {
            "bores" : bores,
            "firstBoreFaces" : qNonCapEntity(id + unstableIdComponent(0), EntityType.FACE)
        };
}

function createBore(context is Context, id is Id, definition is map, plane is Plane, extrudeDistance is ValueWithUnits) returns Query
{
    sketchBoreProfile(context, id + "profile", definition, plane);
    opExtrude(context, id + "extrude", {
                "entities" : qCreatedBy(id + "profile", EntityType.FACE),
                "direction" : plane.normal,
                "endBound" : BoundingType.BLIND,
                "endDepth" : extrudeDistance / 2,
                "startBound" : BoundingType.BLIND,
                "startDepth" : extrudeDistance / 2
            });
    if (definition.boreType == BoreType.SPLINE && definition.offsetBoreProfile)
    {
        opOffsetFace(context, id + "offsetFace", {
                    "moveFaces" : qNonCapEntity(id + "extrude", EntityType.FACE),
                    "offsetDistance" : getBoreProfileOffset(definition)
                });
    }
    return qCreatedBy(id + "extrude", EntityType.BODY);
}

function sketchBoreProfile(context is Context, id is Id, definition is map, plane is Plane)
{
    const sketch = newSketchOnPlane(context, id + "profile", { "sketchPlane" : plane });

    if (definition.boreType == BoreType.HEX)
    {
        const hexRadius = (definition.hexWidth / 2) / cos(30 * degree);
        skRegularPolygon(sketch, "hex", {
                    "center" : zeroVector(2) * meter,
                    "firstVertex" : vector(hexRadius, 0 * meter),
                    "sides" : 6
                });
    }
    else if (definition.boreType == BoreType.HOLE)
    {
        skCircle(sketch, "circle", {
                    "center" : zeroVector(2) * meter,
                    "radius" : definition.holeDiameter
                });
    }
    else if (definition.boreType == BoreType.SPLINE)
    {
        skSplineProfile(sketch, "spline", {
                    "splineType" : definition.splineType,
                    "location" : zeroVector(2) * meter
                });
    }

    skSolve(sketch);
}

function addBoreDebugEntities(context is Context, id is Id, definition is map, pulleyDefinitions is array)
{
    try
    {
        const errorId = id + "temp";
        const profileOffset = getPulleyProfileOffset(definition);
        const pulleys = createPulleys(context, errorId + "pulley", pulleyDefinitions, profileOffset);
        if (definition.addFlanges)
        {
            addFlanges(context, errorId + "flange", pulleyDefinitions, pulleys, definition.flangeSize, definition.unitSystem, profileOffset);
        }
        addDebugEntities(context, qUnion(pulleys), DebugColor.BLUE);

        const boreResult = createBores(context, errorId + "bore", definition, pulleyDefinitions, pulleys);
        addDebugEntities(context, qUnion(boreResult.bores));
    }
}

function getBoreProfileOffset(definition is map)
{
    if (!definition.offsetBoreProfile)
    {
        return 0 * meter;
    }
    return definition.boreOffsetDistance * (definition.boreOppositeDirection ? -1 : 1);
}

const BORE_PROFILE_OFFSET_MANIPULATOR = "boreProfileOffsetManipulator";

/**
 * Returns the current profile offset of the pulley.
 */
function getPulleyProfileOffset(definition is map) returns ValueWithUnits
{
    if (!definition.offsetProfile)
    {
        return 0 * meter;
    }
    return definition.offsetDistance * (definition.oppositeDirection ? -1 : 1);
}

function addText(context is Context, id is Id, definition is map, pulleyDefinitions is array, pulleys is array)
{
    const textId = id + "text";
    createAllText(context, textId, definition, pulleyDefinitions);

    const textFaces = startTracking(context, qCreatedBy(textId, EntityType.FACE));
    try
    {
        opBoolean(context, id + "cutText", {
                    "tools" : qCreatedBy(textId, EntityType.BODY)->qBodyType(BodyType.SOLID),
                    "targets" : qUnion(pulleys),
                    "operationType" : BooleanOperationType.SUBTRACTION,
                    "targetsAndToolsNeedGrouping" : true
                });
    }
    catch
    {
        throwTextError(context, id + "error", definition, pulleyDefinitions);
    }

    if (isQueryEmpty(context, textFaces))
    {
        throwTextError(context, id + "error", definition, pulleyDefinitions);
    }
}

function createAllText(context is Context, id is Id, definition is map, pulleyDefinitions is array)
{
    for (var i, pulleyDefinition in pulleyDefinitions)
    {
        const textId = id + unstableIdComponent(i);
        setExternalDisambiguation(context, textId, pulleyDefinition.identity);
        createText(context, textId, definition, pulleyDefinition);
    }
    cleanup(context, id + "delete", qCreatedBy(id, EntityType.BODY)->qSketchFilter(SketchObject.YES));
}

function createText(context is Context, id is Id, definition is map, pulleyDefinition is PulleyDefinition)
{
    var text = pulleyDefinition.teeth ~ "T";

    const plane = pulleyDefinition.plane;

    const pulleyWidth = getPulleyWidth(definition, pulleyDefinition.beltSize, pulleyDefinition.twoBelts);
    var textPlane = plane;
    textPlane.origin += textPlane.normal * pulleyWidth / 2;

    const flangeRadius = getFlangeRadius(definition, pulleyDefinition.beltSize, pulleyDefinition.teeth);
    const textPosition = flangeRadius * definition.textPosition;

    textPlane.origin += textPlane->yAxis() * textPosition;
    const sketchRegions = opText(context, id + "text", {
                "text" : text,
                "height" : getTextHeight(definition),
                "plane" : textPlane
            });

    opExtrude(context, id + "extrudeText", {
                "entities" : sketchRegions,
                "direction" : -textPlane.normal,
                "endBound" : BoundingType.BLIND,
                "endDepth" : getTextDepth(definition)
            });

    if (definition.engraveBothSides)
    {
        var oppositeTextPlane = plane;
        oppositeTextPlane.origin -= oppositeTextPlane.normal * pulleyWidth / 2;
        oppositeTextPlane.origin += oppositeTextPlane->yAxis() * textPosition;

        const sketchRegions = opText(context, id + "oppositeText", {
                    "text" : text,
                    "height" : getTextHeight(definition),
                    "plane" : oppositeTextPlane,
                    "mirrorHorizontal" : true
                });

        opExtrude(context, id + "extrudeOppositeText", {
                    "entities" : sketchRegions,
                    "direction" : textPlane.normal,
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : getTextDepth(definition)
                });
    }

    opPattern(context, id + "copyText", {
                "entities" : qCreatedBy(id, EntityType.BODY)->qBodyType(BodyType.SOLID),
                "transforms" : [rotationAround(line(plane.origin, plane.normal), 180 * degree)],
                "instanceNames" : ["copy"]
            });
}

function throwTextError(context is Context, id is Id, definition is map, pulleyDefinitions is array)
{
    try
    {
        const profileOffset = getPulleyProfileOffset(definition);
        const pulleys = createPulleys(context, id + "pulley", pulleyDefinitions, profileOffset);
        addFlanges(context, id + "flange", pulleyDefinitions, pulleys, definition.flangeSize, definition.unitSystem, profileOffset);

        if (definition.addBore)
        {
            addBores(context, id, definition, pulleyDefinitions, pulleys);
        }
        addDebugEntities(context, qUnion(pulleys), DebugColor.BLUE);

        createAllText(context, id + "text", definition, pulleyDefinitions);
        addDebugEntities(context, qCreatedBy(id + "text", EntityType.BODY));
    }
    throw regenError("Failed to engrave text. Check input.");
}


const TEXT_POSITION_MANIPULATOR = "textPositionManipulator";

function addTextPositionManipulator(context is Context, id is Id, definition is map, pulleyDefinition is PulleyDefinition)
{
    const pulleyWidth = getPulleyWidth(definition, pulleyDefinition.beltSize, pulleyDefinition.twoBelts);
    var textPlane = pulleyDefinition.plane;
    textPlane.origin += textPlane.normal * pulleyWidth / 2;

    const flangeRadius = getFlangeRadius(definition, pulleyDefinition.beltSize, pulleyDefinition.teeth);

    addManipulators(context, id, {
                (TEXT_POSITION_MANIPULATOR) : linearManipulator({
                        "base" : textPlane.origin,
                        "direction" : textPlane->yAxis(),
                        "offset" : flangeRadius * definition.textPosition,
                        "minValue" : 0 * meter,
                        "maxValue" : flangeRadius,
                        "style" : ManipulatorStyleEnum.TANGENTIAL,
                        "primaryParameterId" : "textPosition"
                    })
            });
}

function textPositionManipulatorChange(context is Context, definition is map, newManipulators is map) returns map
{
    const manipulator = newManipulators[TEXT_POSITION_MANIPULATOR];
    if (manipulator == undefined)
    {
        return definition;
    }

    var pulleyTeeth;
    var beltSize;
    if (definition.creationMethod == CreationMethod.MANUAL)
    {
        beltSize = getBeltSize(definition);
        pulleyTeeth = definition.pulleyTeeth;
    }
    else
    {
        const attribute = getBeltPulleyFaceAttribute(context, definition.beltSelections->qNthElement(0));
        if (attribute == undefined || attribute.pulleyType == PulleyType.IDLER)
        {
            return definition;
        }
        beltSize = attribute.beltSize;
        pulleyTeeth = attribute.pulleyTeeth;
    }

    definition.textPosition = roundToPrecision(manipulator.offset / getFlangeRadius(definition, beltSize, pulleyTeeth), 2);
    return definition;
}

export function robotPulleyManipulatorChange(context is Context, definition is map, newManipulators is map) returns map
{
    definition = profileOffsetManipulatorChange(definition, newManipulators[PROFILE_OFFSET_MANIPULATOR], "oppositeDirection");
    definition = profileOffsetManipulatorChange(definition, newManipulators[BORE_PROFILE_OFFSET_MANIPULATOR], "boreOppositeDirection");
    definition = startOffsetManipulatorChange(definition, newManipulators);
    definition = pointManipulatorChange(definition, newManipulators);
    definition = textPositionManipulatorChange(context, definition, newManipulators);
    return definition;
}


