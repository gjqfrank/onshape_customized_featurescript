FeatureScript 2559;
import(path : "onshape/std/common.fs", version : "2559.0");
import(path : "onshape/std/chamfertype.gen.fs", version : "2559.0");

import(path : "472bc4c291e1d2d6f9b98937", version : "a6f978ca56ddf7d00b21ec13");
import(path : "452d43a015d17145ad7775e4", version : "1b347bbc84f2742e327113f2");
import(path : "5bee4cc7b6b0575cdb535750", version : "6ff235e17ff476fe00a8f93d");
import(path : "ea127c07807644fb48d3a1ae", version : "d1ca55c12bc8b5f162467e5b");

export import(path : "4d2d3f0157d54e1b6a06420a", version : "4ab45fce4467c21e7843268b");

import(path : "6c65805103086c85362ee4b7", version : "57b72bf75da88e7f13c77c27");

// Custom icon by Eliza Barnett of Team 1745
Icon::import(path : "f7208b20c64f74041af4afa2", version : "3326cee850e70c8a170b912d");

annotation {
        "Feature Type Name" : "Robot belt",
        "Editing Logic Function" : "robotBeltEditLogic",
        "Manipulator Change Function" : "robotBeltManipulatorChange",
        "Icon" : Icon::BLOB_DATA,
        "Feature Type Description" : "Create GT2, HTD, and RT25 belts and position and size them automatically based on model geometry." ~
        "<br>See also the Robot pulley and Robot belt tuner FeatureScripts, which work with this feature directly." ~ CREDIT
    }
export const frcBeltCalculator = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        robotBeltPredicate(definition);
    }
    {
        doRobotBelt(context, id, definition);
    });

function doRobotBelt(context is Context, id is Id, definition is map)
{
    var beltDefinition;
    if (isSimpleBelt(definition))
    {
        beltDefinition = getSimpleBeltDefinition(context, id, definition);
    }
    else
    {
        beltDefinition = getComplexBeltDefinition(context, id, definition);
    }

    addStartOffsetManipulator(context, id, definition, beltDefinition.beltPlane);

    const sketchBeltResult = sketchBelt(context, id + "sketch", beltDefinition);
    const counterClockwise = sketchBeltResult.counterClockwise;
    const beltLoop = sketchBeltResult.beltLoop;
    const arcQueries = sketchBeltResult.arcQueries;

    if (isComplexBelt(definition))
    {
        addBeltFlipManipulators(context, id, beltDefinition, arcQueries, counterClockwise);
        validateComplexBeltLength(context, id, definition, beltLoop, beltDefinition);
    }

    createBelt(context, id + "belt", definition, beltDefinition, beltLoop, counterClockwise, arcQueries);
    const belt = qCreatedBy(id + "belt", EntityType.BODY);

    if (definition.addMateConnectors)
    {
        createMateConnectors(context, id + "mateConnectors", beltDefinition, belt);
    }
    cleanup(context, id + "delete", qCreatedBy(id + "sketch", EntityType.BODY));
}

/**
 * A type defining belt information used for modeling.
 * @type {{
 *      @field beltPlane {Plane} : A plane defining the position of the belt.
 *              Its `x` axis should be arbitrary in order to be consistent with other belts.
 * }}
 */
type BeltDefinition typecheck canBeBeltDefinition;

export predicate canBeBeltDefinition(value)
{
    value.beltPlane is Plane;
    value.beltSize is BeltSize;
    value.beltTeeth is number;

    value.pulleyDefinitions is array;
    for (var pulleyDefinition in value.pulleyDefinitions)
    {
        pulleyDefinition is PulleyDefinition;
    }
}

/**
 * A type defining pulley information used for modeling.
 *
 * @type {{
 *      @field location {Vector} : A 2D point representing the location of the pulley relative to the belt plane.
 * }}
 */
type PulleyDefinition typecheck canBePulleyDefinition;

export predicate canBePulleyDefinition(value)
{
    value.identity is Query || value.identity is undefined;
    value.location is Vector;
    value.pulleyType is PulleyType;
    if (isPulley(value.pulleyType))
    {
        value.pulleyTeeth is number;
    }
    else
    {
        value.idlerRadius is ValueWithUnits;
    }
}

function getPulleyType(definition is map, pulley is map) returns PulleyType
{
    if (pulley.beltSide == BeltSide.INSIDE)
    {
        return PulleyType.INSIDE_PULLEY;
    }
    return isDoubleSidedBelt(definition) ? PulleyType.OUTSIDE_PULLEY : PulleyType.IDLER;
}

function getBoundaryCircle(pulleyDefinition is PulleyDefinition, beltSize is BeltSize) returns BoundaryCircle
{
    var radius;
    if (isPulley(pulleyDefinition.pulleyType))
    {
        radius = getPulleyRadius(getBeltPitch(beltSize), pulleyDefinition.pulleyTeeth);
    }
    else
    {
        radius = pulleyDefinition.idlerRadius + getBeltOusideThickness(beltSize);
    }
    return {
                "location" : pulleyDefinition.location,
                "identity" : pulleyDefinition.identity,
                "radius" : radius,
                "flipped" : isOutside(pulleyDefinition.pulleyType)
            } as BoundaryCircle;
}

function getComplexBeltDefinition(context is Context, id is Id, definition is map)
{
    verifyNonemptyArray(context, definition, "pulleys", "Add pulley locations.");
    const pulleys = definition.pulleys;
    if (size(pulleys) < 2)
    {
        throw regenError("Add at least two pulleys.", "pulleys");
    }

    for (var i, pulley in pulleys)
    {
        if (isQueryEmpty(context, pulley.pulleyLocation))
        {
            throw regenError("Select a pulley location.", [arrayParameterId("pulleys", i, "pulleyLocation")]);
        }
    }

    const beltPlane = getBeltPlane(context, definition);
    var pulleyDefinitions = [];
    for (var pulley in pulleys)
    {
        const point = evVertexPoint(context, { "vertex" : pulley.pulleyLocation });
        var pulleyDefinition = {
            // worldToPlane projects the point onto the plane
            "identity" : pulley.pulleyLocation,
            "location" : worldToPlane(beltPlane, point),
            "pulleyType" : getPulleyType(definition, pulley)
        };

        if (isPulley(pulleyDefinition.pulleyType))
        {
            pulleyDefinition.pulleyTeeth = pulley.pulleyTeeth;
        }
        else
        {
            pulleyDefinition.idlerRadius = pulley.idlerDiameter / 2;
        }
        pulleyDefinitions = append(pulleyDefinitions, pulleyDefinition as PulleyDefinition);
    }

    const beltSize = getBeltSize(definition);
    const beltTeeth = getBeltTeeth(definition);
    // if (autoBelt(definition))
    // {
    //     const circles = getBoundaryCircles(pulleyDefinitions, beltSize);
    //     const locations = extractFromArrayOfMaps(circles, "location");
    //     const beltLength = computeBeltLength(circles, isCounterClockwise(locations));
    //     const targetTeeth = beltLength / getBeltPitch(beltSize);
    //     beltTeeth = getClosestBeltTeeth(definition, targetTeeth);
    // }

    return {
                "beltPlane" : beltPlane,
                "beltSize" : beltSize,
                "beltTeeth" : beltTeeth,
                "pulleyDefinitions" : pulleyDefinitions
            } as BeltDefinition;
}

/**
 * Computes the belt definition.
 *
 * Note error handling is performed in the following order:
 * 1. Selection errors.
 * 2. Center to center distance errors.
 */
function getSimpleBeltDefinition(context is Context, id is Id, definition is map) returns BeltDefinition
precondition
{
    isTopLevelId(id);
}
{
    if (definition.hasSelections)
    {
        if (isQueryEmpty(context, qUnion(definition.pulleyOneLocation, definition.pulleyTwoLocation)))
        {
            throw regenError("Select pulley positions.", ["pulleyOneLocation", "pulleyTwoLocation"]);
        }
        verifyNonemptyQuery(context, definition, "pulleyOneLocation", "Select a position for pulley one.");
        verifyNonemptyQuery(context, definition, "pulleyTwoLocation", "Select a position for pulley two.");
    }

    const beltPlane = getBeltPlane(context, definition);
    const secondPoint = projectSecondPoint(context, definition, beltPlane);
    const secondPointVector = secondPoint - beltPlane.origin;

    const beltSize = getBeltSize(definition);
    const pulleyTeeth = getPulleyTeeth(definition);

    const pitch = getBeltPitch(beltSize);
    const measuredCenterToCenter = norm(secondPointVector);

    const beltTeeth = getBeltTeeth(definition);
    // if (autoBelt(definition))
    // {
    //     const targetTeeth = computeBeltTeeth(pitch, pulleyTeeth, measuredCenterToCenter);
    //     beltTeeth = getClosestBeltTeeth(definition, targetTeeth);
    // }

    const idealCenterToCenter = computeBeltCenterToCenter(pitch, beltTeeth, pulleyTeeth) + definition.centerToCenterAdjustment;
    var modelCenterToCenter;
    if (validateCenterToCenter(context, id, definition, beltTeeth, measuredCenterToCenter, idealCenterToCenter))
    {
        // Use the measured center to center as the model center to center so assemblies and things line up properly
        modelCenterToCenter = measuredCenterToCenter;
    }
    else
    {
        // If the measured center to center is way off, we fall back to the ideal center to center
        modelCenterToCenter = idealCenterToCenter;
    }

    const pulleyDefinitions = [
            {
                    "location" : zeroVector(2) * meter,
                    "identity" : (definition.hasSelections ? definition.pulleyOneLocation : undefined),
                    "pulleyType" : PulleyType.INSIDE_PULLEY,
                    "pulleyTeeth" : pulleyTeeth[0],
                } as PulleyDefinition,
            {
                    // Remake the second point so it's properly on the beltPlane and accounts for differing center to center
                    "location" : worldToPlane(beltPlane, beltPlane.origin + normalize(secondPointVector) * modelCenterToCenter),
                    "identity" : (definition.hasSelections ? definition.pulleyTwoLocation : undefined),
                    "pulleyType" : PulleyType.INSIDE_PULLEY,
                    "pulleyTeeth" : pulleyTeeth[1],
                } as PulleyDefinition
        ];

    return {
                "beltPlane" : beltPlane,
                "beltMode" : BeltMode.SIMPLE,
                "beltSize" : beltSize,
                "beltTeeth" : beltTeeth,
                "pulleyDefinitions" : pulleyDefinitions
            } as BeltDefinition;
}

/**
 * Computes the belt plane.
 */
function getBeltPlane(context is Context, definition is map) returns Plane
{
    if (isSimpleBelt(definition) && !definition.hasSelections)
    {
        return XY_PLANE;
    }
    const location = isSimpleBelt(definition) ? definition.pulleyOneLocation : definition.pulleys[0].pulleyLocation;
    const beltPlane = evVertexCoordSystem(context, { "vertex" : location })->plane();
    return applyStartOffset(context, definition, beltPlane);
}

/**
 * Computes the intersection of the second point with the belt plane.
 */
function projectSecondPoint(context is Context, definition is map, beltPlane is Plane) returns Vector
{
    if (!definition.hasSelections)
    {
        // Exact point doesn't matter since no ctc check
        return beltPlane.x * meter;
    }

    var secondPoint;
    if (isVertex(context, definition.pulleyTwoLocation) || isMateConnector(context, definition.pulleyTwoLocation))
    {
        const basePoint = evVertexCoordSystem(context, { "vertex" : definition.pulleyTwoLocation }).origin;
        // We need to project the point and convert it to plane coordinates
        // We project it so it's in the right spot relative to the baseBeltPlane
        secondPoint = project(beltPlane, basePoint);
    }
    else
    {
        const axis = evAxis(context, { "axis" : definition.pulleyTwoLocation });
        // Required to prevent issues with offsets causing the pulley to move
        if (!perpendicularVectors(axis.direction, beltPlane.normal))
        {
            throw regenError("The selected axis must be perpendicular to the belt plane.", ["pulleyOneLocation", "pulleyTwoLocation"], qUnion(definition.pulleyOneLocation, definition.pulleyTwoLocation));
        }
        const axisIntersection = intersection(beltPlane, axis);
        if (axisIntersection.dim == 1)
        {
            throw regenError("The selected axis is collinear with the belt plane.",
                ["pulleyOneLocation", "pulleyTwoLocation"], qUnion(definition.pulleyOneLocation, definition.pulleyTwoLocation));
        }
        else if (axisIntersection.dim == -1)
        {
            throw regenError("The selected axis does not intersect the belt plane.",
                ["pulleyOneLocation", "pulleyTwoLocation"], qUnion(definition.pulleyOneLocation, definition.pulleyTwoLocation));
        }
        secondPoint = axisIntersection.intersection;
    }

    if (tolerantEquals(secondPoint, beltPlane.origin))
    {
        throw regenError("The selected pulley locations cannot be coincident.",
            ["pulleyOneLocation", "pulleyTwoLocation"], qUnion(definition.pulleyOneLocation, definition.pulleyTwoLocation));
    }
    return secondPoint;
}

/**
 * Reports a warning if the measuredCenterToCenter does not closely match the idealCenterToCenter.
 */
function validateCenterToCenter(context is Context, id is Id, definition is map, teeth is number, measuredCenterToCenter is ValueWithUnits, idealCenterToCenter is ValueWithUnits) returns boolean
{
    if (!definition.hasSelections)
    {
        /**
         * The selected 100T belt has a 3.45 in center to center distance.
         */
        reportFeatureInfo(context, id, "The selected " ~ teeth ~ "T belt has a " ~ makeValueString(definition.unitSystem, idealCenterToCenter) ~ " center to center distance.");
        return true;
    }

    // Strict matching?
    if (definition.disableBeltValidation || withinDisplayPrecision(definition, measuredCenterToCenter, idealCenterToCenter))
    {
        // reportFeatureInfo(context, id, "The selected " ~ teeth ~ "T belt matches the distance between selections.");
        return true;
    }

    // Note: the following strings don't end with a period to make copy-pasting easier
    const centerToCenterString = makeValueString(definition.unitSystem, idealCenterToCenter);
    /**
     * The distance between your selections does not match the center to center distance of the selected belt.
     * Update the part studio so the distance between selections is 4.5 in
     */
    reportFeatureWarning(context, id, "To use this belt, update the part studio so the distance between your selections is " ~ centerToCenterString);
    return false;
}

function validateComplexBeltLength(context is Context, id is Id, definition is map, beltLoop is Query, beltDefinition is BeltDefinition)
{
    if (definition.disableBeltValidation)
    {
        return;
    }
    const actualLength = evLength(context, { "entities" : beltLoop });
    const idealLength = (getBeltPitch(beltDefinition.beltSize) * beltDefinition.beltTeeth) + definition.beltFitAdjustment;

    if (withinDisplayPrecision(definition, actualLength, idealLength))
    {
        reportFeatureInfo(context, id, "Your selections match the selected " ~ beltDefinition.beltTeeth ~ "T belt. Note you may still want to design an adjustable tensioner to ensure proper fit.");
        return;
    }

    const difference = idealLength - actualLength;

    var verbs;
    const tooLong = difference > 0 * meter;
    if (tooLong)
    {
        verbs = {
                "longOrShort" : "long",
                "smallerOrLarger" : "smaller",
                "increaseOrDecrease" : "increasing"
            };
    }
    else
    {
        verbs = {
                "longOrShort" : "short",
                "smallerOrLarger" : "larger",
                "increaseOrDecrease" : "decreasing"
            };
    }


    /**
     * The closest belt is 100T and is 4.5 in too long./The selected belt is 4.5 in too long.
     * To use this belt, get the distance close by selecting a smaller belt and/or increasing the total perimeter, then use the Robot belt tuner FeatureScript to compute an exact solution.
     */
    var message = "The selected belt is ";
    message ~= makeValueString(definition.unitSystem, abs(difference)) ~ " too " ~ verbs.longOrShort;
    message ~= ". ";
    message ~= "To use this belt, get the distance close by selecting a " ~ verbs.smallerOrLarger ~ " belt and/or " ~ verbs.increaseOrDecrease ~ " the total perimeter, then use the Robot belt tuner FeatureScript to compute an exact solution.";
    reportFeatureWarning(context, id, message);
}


function getPulleyTeeth(definition is map) returns array
{
    return mapArray(values(Pulley), function(pulley is Pulley)
        {
            return definition[pulleyString(pulley) ~ "Teeth"];
        });
}

function getPulleyDiameters(pitch is ValueWithUnits, pulleyTeethArray is array)
{
    return mapArray(pulleyTeethArray, function(pulleyTeeth)
        {
            return getPulleyDiameter(pitch, pulleyTeeth);
        });
}

/**
 * Returns the center to center distance of an arbitrary two pulley belt.
 */
function computeBeltCenterToCenter(pitch is ValueWithUnits, teeth is number, pulleyTeethArray is array) returns ValueWithUnits
precondition
{
    size(pulleyTeethArray) == 2;
}
{
    const pulleyDiameters = getPulleyDiameters(pitch, pulleyTeethArray);
    const largePulleyDiameter = max(pulleyDiameters);
    const smallPulleyDiameter = min(pulleyDiameters);

    try
    {
        const term = (teeth * pitch - (PI / 2 * (largePulleyDiameter + smallPulleyDiameter))) / 4;
        return term + sqrt(term ^ 2 - ((largePulleyDiameter - smallPulleyDiameter) ^ 2) / 8);
    }
    catch
    {
        throw regenError("Your selected belt is too small.", ["pulleyOneTeeh", "pulleyTwoTeeth", "beltTeeth"]);
    }
}

/**
 * Computes the number of teeth required to achieve the specified `targetCenterToCenter` distance.
 * Note the number of teeth may be a fraction.
 */
function computeBeltTeeth(pitch is ValueWithUnits, pulleyTeethArray is array, targetCenterToCenter is ValueWithUnits) returns number
precondition
{
    size(pulleyTeethArray) == 2;
}
{
    const pulleyDiameters = getPulleyDiameters(pitch, pulleyTeethArray);
    const largePulleyDiameter = max(pulleyDiameters);
    const smallPulleyDiameter = min(pulleyDiameters);

    const term = -largePulleyDiameter * smallPulleyDiameter +
        4 * targetCenterToCenter ^ 2 +
        largePulleyDiameter * targetCenterToCenter * PI +
        smallPulleyDiameter * targetCenterToCenter * PI;
    const numerator = (largePulleyDiameter ^ 2 + smallPulleyDiameter ^ 2 + 2 * term);
    return numerator / (4 * targetCenterToCenter * pitch);
}

/**
 * Creates the belt.
 * @param id : @autocomplete `id + "belt"`
 */
function createBelt(context is Context, id is Id, definition is map, beltDefinition is BeltDefinition, beltLoop is Query, counterClockwise is boolean, arcQueries is array)
{
    const pulleyDefinitions = beltDefinition.pulleyDefinitions;
    const arcTrackingQueries = mapArray(arcQueries, function(arcQuery)
        {
            return startTracking(context, arcQuery);
        });

    const beltAttribute = {
                "beltMode" : definition.beltMode,
                "modelBeltTeeth" : definition.modelBeltTeeth,
                "isDoubleSidedBelt" : isDoubleSidedBelt(definition),
                "beltSize" : beltDefinition.beltSize,
                "beltTeeth" : beltDefinition.beltTeeth
            } as BeltAttribute;

    const result = extrudeBelt(context, id + "belt", beltAttribute, beltDefinition.beltPlane, beltLoop, counterClockwise);

    setAttribute(context, {
                "entities" : result.startFace,
                "name" : BELT_START_FACE_ATTRIBUTE,
                "attribute" : {}
            });

    setAttribute(context, {
                "entities" : result.belt,
                "name" : BELT_ATTRIBUTE,
                "attribute" : beltAttribute
            });

    for (var i, trackedArcQuery in arcTrackingQueries)
    {
        const pulleyDefinition = beltDefinition.pulleyDefinitions[i];

        const arcFaces = trackedArcQuery->qEntityFilter(EntityType.FACE);
        var closestFace = qClosestTo(arcFaces, planeToWorld(beltDefinition.beltPlane, pulleyDefinition.location));
        var furthestFace = arcFaces->qSubtraction(closestFace);
        setAttribute(context, {
                    "entities" : closestFace,
                    "name" : BELT_PULLEY_FACE_ATTRIBUTE,
                    "attribute" : {
                            "beltSize" : beltDefinition.beltSize,
                            "pulleyType" : pulleyDefinition.pulleyType,
                            "pulleyTeeth" : pulleyDefinition.pulleyTeeth,
                            "idlerRadius" : pulleyDefinition.idlerRadius,
                            "beltSide" : pulleyDefinition.beltSide,
                        } as BeltFaceAttribute
                });

        setAttribute(context, {
                    "entities" : furthestFace,
                    "name" : BELT_PULLEY_FACE_ATTRIBUTE,
                    "attribute" : {
                            "beltSize" : beltDefinition.beltSize,
                            "pulleyType" : pulleyDefinition.pulleyType,
                            "pulleyTeeth" : pulleyDefinition.pulleyTeeth,
                            "idlerRadius" : pulleyDefinition.idlerRadius,
                            "beltSide" : pulleyDefinition.beltSide,
                        } as BeltFaceAttribute
                });

        setAttribute(context, {
                    "entities" : furthestFace->qNthElement(0),
                    "name" : BELT_PRIMARY_PULLEY_FACE_ATTRIBUTE,
                    "attribute" : {}
                });
    }

    setBeltProperties(context, result.belt, beltAttribute);
}


/**
 * Sketches the main interior loop of a belt. Returns a query for the loop and an ordered array of queries for each arc.
 */
function sketchBelt(context is Context, id is Id, beltDefinition is BeltDefinition) returns map
{
    const beltPlane = beltDefinition.beltPlane;

    var circles = getBoundaryCircles(beltDefinition.pulleyDefinitions, beltDefinition.beltSize);
    const locations = extractFromArrayOfMaps(circles, "location");
    const counterClockwise = isCounterClockwise(locations);
    const connectingPointsArray = getPulleyConnectingPointsArray(circles, counterClockwise);
    // robustness requirements are still high, even for a belt
    const arcQueries = sketchArcs(context, id + "arcs", circles, beltPlane, connectingPointsArray, counterClockwise);

    var identities;
    // If the belt only has 2 pulleys, we can't use identities for lines since the edges become indistinguishable to Onshape
    if (size(circles) == 2)
    {
        identities = makeArray(2, undefined);
    }
    else
    {
        identities = extractFromArrayOfMaps(circles, "identity");
    }
    sketchConnectingLines(context, id + "lines", identities, beltPlane, connectingPointsArray);

    return {
            "beltLoop" : qCreatedBy(id, EntityType.EDGE)->qSketchFilter(SketchObject.YES),
            "arcQueries" : arcQueries,
            "counterClockwise" : counterClockwise
        };
}

/**
 * Extracts an array of circle definitions which can be consumed by boundary sketching utilities.
 */
function getBoundaryCircles(pulleyDefinitions is array, beltSize is BeltSize) returns array
{
    return mapArray(pulleyDefinitions, function(pulleyDefinition)
        {
            return getBoundaryCircle(pulleyDefinition, beltSize);
        });
}

/**
 * @return {array} : An array of queries, one for each arc.
 */
function sketchArcs(context is Context, id is Id, circles is array, beltPlane is Plane, connectingPointsArray is array, counterClockwise is boolean) returns array
{
    var arcQueries = [];
    for (var i, curr in circles)
    {
        const nextIndex = getNext(size(circles), i);
        const next = circles[nextIndex];

        if (curr.identity != undefined)
        {
            setExternalDisambiguation(context, id + unstableIdComponent(i), curr.identity);
        }
        const autoSketch = newSketchOnPlane(context, id + unstableIdComponent(i), { "sketchPlane" : beltPlane });

        const fourPoints = concatenateArrays([connectingPointsArray[i], connectingPointsArray[nextIndex]]);
        addArc(autoSketch, "arc", fourPoints, curr, counterClockwise);
        skSolve(autoSketch);
        arcQueries = append(arcQueries, sketchEntityQuery(id + unstableIdComponent(i), EntityType.EDGE, "arc"));
    }
    return arcQueries;
}

function getPulleyConnectingPointsArray(circles is array, counterClockwise is boolean) returns array
{
    return mapArrayIndices(circles, function(i)
        {
            const prev = getPrevious(circles, i);
            const curr = circles[i];
            return circleToCircle(prev, curr, counterClockwise);
        });
}

function setBeltProperties(context is Context, beltQuery is Query, beltAttribute is BeltAttribute)
{
    setProperty(context, {
                "entities" : beltQuery,
                "propertyType" : PropertyType.MATERIAL,
                "value" : material("Viton Rubber", 1827 * kilogram / meter ^ 3) // default belt material and density
            });
    setProperty(context, {
                "entities" : beltQuery,
                "propertyType" : PropertyType.APPEARANCE,
                "value" : color(135 / 255, 135 / 255, 135 / 255)
            });

    const beltName = getBeltName(beltAttribute.beltTeeth, beltAttribute.isDoubleSidedBelt, beltAttribute.beltSize);
    setProperty(context, {
                "entities" : beltQuery,
                "propertyType" : PropertyType.NAME,
                "value" : beltName
            });
}

function getBeltName(beltTeeth is number, isDoubleSidedBelt is boolean, beltSize is BeltSize) returns string
{
    // 24T Double Sided GT2 Belt
    return beltTeeth ~ "T " ~ (isDoubleSidedBelt ? "Double Sided " : "") ~ getBeltType(beltSize) ~ " Belt";
}


function createMateConnectors(context is Context, id is Id, beltDefinition is BeltDefinition, belt is Query)
{
    var coordSystem = beltDefinition.beltPlane->coordSystem();
    for (var i, pulleyDefinition in beltDefinition.pulleyDefinitions)
    {
        const mateConnectorId = id + unstableIdComponent(i);
        setExternalDisambiguation(context, mateConnectorId, pulleyDefinition.identity);

        const location = planeToWorld(beltDefinition.beltPlane, pulleyDefinition.location);
        coordSystem.origin = location;
        opMateConnector(context, mateConnectorId, {
                    "coordSystem" : coordSystem,
                    "owner" : belt
                });
        setAttribute(context, {
                    "entities" : qCreatedBy(mateConnectorId, EntityType.BODY),
                    "name" : BELT_PULLEY_FACE_ATTRIBUTE,
                    "attribute" : {
                            "pulleyType" : pulleyDefinition.pulleyType,
                            "beltSize" : beltDefinition.beltSize,
                            "pulleyTeeth" : pulleyDefinition.pulleyTeeth,
                            "idlerRadius" : pulleyDefinition.idlerRadius,
                            "outerFace" : false
                        } as BeltFaceAttribute
                });
    }
}

const BELT_SIDE_FLIP_MANIPULATOR = "beltSideFlipManipulator";

function addBeltFlipManipulators(context is Context, id is Id, beltDefinition is BeltDefinition, arcQueries is array, counterClockwise is boolean)
{
    var manipulators = {};
    for (var i, pulleyDefinition in beltDefinition.pulleyDefinitions)
    {
        const pulleyType = pulleyDefinition.pulleyType;
        const beltSide = pulleyDefinition.beltSide;
        const arcQuery = arcQueries[i];

        const tangentLine = evEdgeTangentLine(context, {
                    "edge" : arcQuery,
                    "parameter" : 0.5
                });
        var base = tangentLine.origin;
        // Points outwards from the belt
        var direction = cross(beltDefinition.beltPlane.normal, tangentLine.direction) * (counterClockwise ? 1 : -1);

        // If idler, adjust base by belt thickness
        if (pulleyDefinition.pulleyType == PulleyType.IDLER)
        {
            // Always draw on inside of belt face
            base += direction * getBeltOusideThickness(beltDefinition.beltSize) * (counterClockwise ? 1 : -1);
        }
        manipulators[BELT_SIDE_FLIP_MANIPULATOR ~ "." ~ i] = flipManipulator({
                    "base" : base,
                    "direction" : direction,
                    "flipped" : false
                });
    }
    addManipulators(context, id, manipulators);
}

export function robotBeltManipulatorChange(context is Context, definition is map, newManipulators is map) returns map
{
    for (var key, manipulator in newManipulators)
    {
        const parsed = match(key, BELT_SIDE_FLIP_MANIPULATOR ~ ".(\\d+)");
        if (parsed.hasMatch && manipulator.flipped)
        {
            const index = stringToNumber(parsed.captures[1]);
            const currentSide = definition.pulleys[index].beltSide;
            // Swap the pulley type each time the manipulator is clicked
            definition.pulleys[index].beltSide = currentSide == BeltSide.INSIDE ? BeltSide.OUTSIDE : BeltSide.INSIDE;
        }
    }
    definition = startOffsetManipulatorChange(definition, newManipulators);
    return definition;
}

export function robotBeltEditLogic(context is Context, id is Id, oldDefinition is map, definition is map, isCreating is boolean, clickedButton is string) returns map
{
    if (oldDefinition == {})
    {
        return definition;
    }

    if (isSimpleBelt(definition))
    {
        definition = fillBeltParameters(context, oldDefinition, definition, Pulley.ONE);
        definition = fillBeltParameters(context, oldDefinition, definition, Pulley.TWO);
    }

    // Automatically choose closest supplier belt when closest belt is turned off
    if (clickedButton == "selectClosestBelt" || beltTypeOrSupplierChanged(oldDefinition, definition))
    {
        definition = setClosestBelt(context, definition);
    }
    return definition;
}

function beltTypeOrSupplierChanged(oldDefinition is map, definition is map) returns boolean
{
    if (oldDefinition.beltType != definition.beltType)
    {
        return true;
    }
    else if (customBelt(oldDefinition) != customBelt(definition))
    {
        return true;   
    }
    return getBeltTeethSupplierKey(oldDefinition) != getBeltTeethSupplierKey(definition);
}

/**
 * Attempts to set the belt size whenever a pulley mate connector is selected.
 */
function fillBeltParameters(context is Context, oldDefinition is map, definition is map, pulley is Pulley) returns map
{
    const locationKey = pulleyString(pulley) ~ "Location";
    if ((oldDefinition == {} || !areQueriesEquivalent(context, oldDefinition[locationKey], definition[locationKey])) && isMateConnector(context, definition[locationKey]))
    {
        const attribute = getAttribute(context, {
                    // Mate connector query parameters need qOwnerBody to get the explicit mate connector
                    "entity" : definition[locationKey]->qOwnerBody(),
                    "name" : PULLEY_ATTRIBUTE
                });
        if (attribute == undefined)
        {
            return definition;
        }
        definition = mergeMaps(definition, beltSizeToBeltParameters(attribute.beltSize));
        definition[pulleyString(pulley) ~ "Teeth"] = attribute.pulleyTeeth;
    }
    return definition;
}

function setClosestBelt(context is Context, definition is map) returns map
{
    if (isQueryEmpty(context, definition.pulleyOneLocation) || isQueryEmpty(context, definition.pulleyTwoLocation))
    {
        return definition;
    }

    try silent
    {
        const beltPlane = getBeltPlane(context, definition);
        const secondPoint = projectSecondPoint(context, definition, beltPlane);
        const secondPointVector = secondPoint - beltPlane.origin;

        const beltSize = getBeltSize(definition);
        const pitch = getBeltPitch(beltSize);

        const pulleyTeeth = getPulleyTeeth(definition);

        const targetCenterToCenter = norm(secondPointVector);
        const targetTeeth = computeBeltTeeth(pitch, pulleyTeeth, targetCenterToCenter);
        const beltTeeth = getClosestBeltTeeth(definition, targetTeeth);

        if (customBelt(definition))
        {
            definition.beltTeeth = beltTeeth;
        }
        else
        {
            const supplierKey = getBeltTeethSupplierKey(definition);
            definition[supplierKey] = "_" ~ beltTeeth;
        }
    }

    return definition;
}

/**
 * Returns the number of teeth the belt should use.
 */
function getClosestBeltTeeth(definition is map, targetTeeth is number) returns number
{
    const table = getBeltTable(definition);
    if (table == undefined)
    {
        return round(targetTeeth);
    }
    const beltArray = mapArray(values(table), function(value is string)
            {
                return extractNumber(value);
            })->sort(function(a, b)
        {
            return a - b;
        });

    var bestBelt = beltArray[0];
    for (var belt in beltArray)
    {
        if (belt <= targetTeeth)
        {
            bestBelt = belt;
        }
        else // belt is larger; terminate search
        {
            // smaller belt, larger belt
            if (abs(bestBelt - targetTeeth) >= abs(belt - targetTeeth))
            {
                bestBelt = belt;
            }
            break;
        }
    }
    return bestBelt;
}
