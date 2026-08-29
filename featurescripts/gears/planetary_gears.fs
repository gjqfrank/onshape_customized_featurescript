FeatureScript 608;
import(path : "onshape/std/geometry.fs", version : "608.0");

export import(path : "5742c8cde4b06c68b362d748/16a222adf3cb9a3b33ed9536/01a666571e625f8b819fd75b", version : "376a1982a13b44699a90596a");


annotation { "Feature Type Name" : "Planetary Gears", "Editing Logic Function" : "editPlanetaryGearLogic" }
export const planetaryGears = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "NeedsRotation", "UIHint" : "ALWAYS_HIDDEN", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        definition.needsRotation is boolean;

        annotation { "Name" : "Number of Sun teeth", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        isInteger(definition.numSunTeeth, SUN_TEETH_BOUNDS);

        annotation { "Name" : "Number of Planet teeth", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        isInteger(definition.numPlanetTeeth, PLANET_TEETH_BOUNDS);

        annotation { "Name" : "Number of Ring teeth", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        isInteger(definition.numRingTeeth, RING_TEETH_BOUNDS);

        annotation { "Name" : "Number of Planets", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        isInteger(definition.numPlanets, PLANET_BOUNDS);

        annotation { "Name" : "Input type", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        definition.GearInputType is GearInputType;

        if (definition.GearInputType == GearInputType.module)
        {
            annotation { "Name" : "Module", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isLength(definition.module, MODULE_BOUNDS);
        }

        if (definition.GearInputType == GearInputType.diametralPitch)
        {
            annotation { "Name" : "Diametral pitch", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isReal(definition.diametralPitch, POSITIVE_REAL_BOUNDS);
        }

        if (definition.GearInputType == GearInputType.circularPitch)
        {
            annotation { "Name" : "Circular pitch", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isLength(definition.circularPitch, LENGTH_BOUNDS);
        }

        annotation { "Name" : "Sun pitch circle diameter", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        isLength(definition.pitchCircleDiameter, LENGTH_BOUNDS);

        annotation { "Name" : "Pressure angle", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        isAngle(definition.pressureAngle, PRESSURE_ANGLE_BOUNDS);

        annotation { "Name" : "Root fillet type", "Default" : RootFilletType.third, "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        definition.rootFillet is RootFilletType;

        annotation { "Name" : "Center bore", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        definition.centerHole is boolean;

        if (definition.centerHole)
        {
            annotation { "Name" : "Bore diameter", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isLength(definition.centerHoleDia, CENTERHOLE_BOUNDS);

            annotation { "Name" : "Keyway", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            definition.key is boolean;

            if (definition.key)
            {
                annotation { "Name" : "Key width", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
                isLength(definition.keyWidth, KEY_BOUNDS);

                annotation { "Name" : "Key height", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
                isLength(definition.keyHeight, KEY_BOUNDS);
            }
        }

        annotation { "Name" : "Select origin position"}
        definition.centerPoint is boolean;

        if (definition.centerPoint)
        {
            annotation { "Name" : "Sketch vertex for center", "Filter" : EntityType.VERTEX && SketchObject.YES, "MaxNumberOfPicks" : 1}
            definition.center is Query;
        }

        annotation { "Name" : "Extrude depth", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        isLength(definition.gearDepth, BLEND_BOUNDS);

        annotation { "Name" : "Extrude direction", "UIHint" : "OPPOSITE_DIRECTION"}
        definition.flipGear is boolean;

        annotation { "Name" : "Backlash", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        definition.backlash is boolean;

        if (definition.backlash)
        {
            annotation { "Name" : "Backlash Value", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isLength(definition.backlashVal, backlash_BOUNDS);

        }


        annotation { "Name" : "Offset", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        definition.offset is boolean;

        if (definition.offset)
        {
            annotation { "Name" : "Root diameter", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isLength(definition.offsetClearance, ZERO_DEFAULT_LENGTH_BOUNDS);

            annotation { "Name" : "Dedendum factor", "Default" : DedendumFactor.d250, "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            definition.dedendumFactor is DedendumFactor;

            annotation { "Name" : "Outside diameter", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isLength(definition.offsetDiameter, ZERO_DEFAULT_LENGTH_BOUNDS);
            
            annotation { "Name" : "Ring Offset", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isLength(definition.ringOffsetClearence, ZERO_DEFAULT_LENGTH_BOUNDS);
            

        }
    }

    {
        checkDefinition(context, id, definition);

        displayReductionInfo(context, id, definition);

        const gearDef = { "GearInputType" : GearInputType.module, "center" : definition.center, "centerHole" : definition.centerHole, centerHoleDia : definition.centerHoleDia, "centerPoint" : false, "circularPitch" : definition.circularPitch, "dedendumFactor" : definition.dedendumFactor, "diametralPitch" : definition.diametricalPitch, "flipGear" : definition.flipGear, "gearDepth" : definition.gearDepth, "key" : definition.key, "keyHeight" : definition.keyHeight, "keyWidth" : definition.keyWidth, "module" : definition.module, /*"numTeeth" : 25, */ "offset" : definition.offset, "offsetAngle" : 0 * degree, "offsetClearance" : definition.offsetClearance, "offsetDiameter" : definition.offsetDiameter, "pressureAngle" : definition.pressureAngle, "rootFillet" : definition.rootFillet };

        var sunDef = { "numTeeth" : definition.numSunTeeth, "pitchCircleDiameter" : definition.module * definition.numSunTeeth };
        sunDef = getSunRotationMap(context, id, definition, sunDef);

        const planetDef = { "numTeeth" : definition.numPlanetTeeth, "pitchCircleDiameter" : definition.module * definition.numPlanetTeeth};
        const ringDef = { "numTeeth" : definition.numRingTeeth, "pitchCircleDiameter" : definition.module * definition.numRingTeeth, "offsetClearance" : -definition.ringOffsetClearence, "centerHole" : false, "offset" : true };

        SpurGear(context, id + "sun", mergeMaps(gearDef, sunDef));
        SpurGear(context, id + "planet", mergeMaps(gearDef, planetDef));
        SpurGear(context, id + "ring", mergeMaps(gearDef, ringDef));

        opTransform(context, id + "transform1", {
                "bodies" : qCreatedBy(id + "planet", EntityType.BODY),
                "transform" : transform(vector(1, 0, 0) * definition.module * (definition.numSunTeeth + definition.numPlanetTeeth) / 2)
        });

        backlashGears(context, id, definition);

        const planetTransform = function(i)
            {
                return rotationAround(line(vector(0, 0, 0) * meter, vector(0, 1, 0) * meter), 360 / definition.numPlanets * (i - 1) * degree);
            };

        const planetSketchQuery = qUnion([qBodyType(qCreatedBy(id + 'planet', EntityType.BODY), BodyType.WIRE), qBodyType(qCreatedBy(id + 'planet', EntityType.BODY), BodyType.POINT)]);

        opPattern(context, id + 'copyPlanets', {
                    'entities' : planetSketchQuery,
                    'transforms' : mapArray(range(2, definition.numPlanets), planetTransform),
                    'instanceNames' : mapArray(range(2, definition.numPlanets), function(i)
                        {
                            return 'planetGear' ~ i;
                        })
                });

        performRingBoolean(context, id, definition);

        performTransform(context, id, definition);
    });

function displayReductionInfo(context is Context, id is Id, definition)
{
    const carrierConstantSunInput = -1 * definition.numSunTeeth / definition.numRingTeeth;
    const ringConstantCarrierInput = 1 + definition.numRingTeeth / definition.numSunTeeth;
    const ringConstantSunInput = 1 / ringConstantCarrierInput;
    const sunConstantCarrierInput = (definition.numSunTeeth + definition.numRingTeeth) / definition.numRingTeeth;

    var outputString = "Reduction if Carrier is held constant and Sun is used as input: " ~ carrierConstantSunInput
    ~ "<br>" ~ "Reduction if Ring is held constant and Carrier is used as input: " ~ ringConstantCarrierInput
    ~ "<br>" ~ "Reduction if Ring is held constant and Sun is used as input: " ~ ringConstantSunInput
    ~ "<br>" ~ "Reduction if Sun is held constant and Carrier is used as input: " ~ sunConstantCarrierInput;

    reportFeatureInfo(context, id, outputString);

}

function performTransform(context is Context, id is Id, definition)
{
    if (definition.centerPoint)
    {
        const newCoordSys = coordSystem(evVertexPoint(context, {
                        "vertex" : definition.center
                    }), evOwnerSketchPlane(context, {
                            "entity" : definition.center
                        }).x, evOwnerSketchPlane(context, {
                            "entity" : definition.center
                        }).normal);

        const rotTransform = rotationAround(line(vector(0, 0, 0) * inch, vector(-1, 0, 0) * inch), 90 * degree);
        const finalTransform = toWorld(newCoordSys) * rotTransform;

        var moveObjects = qUnion([qCreatedBy(id + "copyPlanets", EntityType.BODY), qCreatedBy(id + "ring", EntityType.BODY), qCreatedBy(id + "sun", EntityType.BODY), qCreatedBy(id + "planet", EntityType.BODY), qCreatedBy(id + "extrude1", EntityType.BODY)]);

        opTransform(context, id + "transformFinal", {
                    "bodies" : moveObjects,
                    "transform" : finalTransform
                });
    }
}


function performRingBoolean(context is Context, id is Id, definition)
{
    var outerRingCircle = newSketch(context, id + "outerCircle", {
            "sketchPlane" : qCreatedBy(makeId("Front"), EntityType.FACE)
        });


    skCircle(outerRingCircle, "circle1", {
                "center" : vector(0, 0) * inch,
                "radius" : definition.module * definition.numRingTeeth / 2 * 1.2
            });

    skSolve(outerRingCircle);

    opExtrude(context, id + "extrude1", {
                "entities" : qCreatedBy(id + 'outerCircle', EntityType.FACE),
                "direction" : evOwnerSketchPlane(context, { "entity" : qCreatedBy(id + 'outerCircle', EntityType.FACE) }).normal * (definition.flipGear ? -1 : 1),
                "endBound" : BoundingType.BLIND,
                "endDepth" : definition.gearDepth
            });
    opDeleteBodies(context, id + "deleteBodies1", {
                "entities" : qCreatedBy(id + 'outerCircle')
            });


    opBoolean(context, id + "ringBoolean", {
                "tools" : qBodyType(qCreatedBy(id + "ring", EntityType.BODY), BodyType.SOLID),
                "targets" : qCreatedBy(id + "extrude1", EntityType.BODY),
                "operationType" : BooleanOperationType.SUBTRACTION
            });
}


function backlashGears(context is Context, id is Id, definition)
{
    var parallelPlane = evPlane(context, {
            "face" : qCreatedBy(makeId("Front"), EntityType.FACE)
        });

    if (definition.backlash)
    {
        opOffsetFace(context, id + "offsetFace1", {
                    "moveFaces" : qSubtraction(qCreatedBy(id + "ring", EntityType.FACE), qParallelPlanes(qCreatedBy(id + "ring", EntityType.FACE), parallelPlane)),
                    "offsetDistance" : definition.backlashVal
                });
        opOffsetFace(context, id + "offsetFace2", {
                    "moveFaces" : qSubtraction(qCreatedBy(id + "planet", EntityType.FACE), qParallelPlanes(qCreatedBy(id + "planet", EntityType.FACE), parallelPlane)),
                    "offsetDistance" : -definition.backlashVal
                });
        opOffsetFace(context, id + "offsetFace3", {
                    "moveFaces" : qSubtraction(qCreatedBy(id + "sun", EntityType.FACE), qParallelPlanes(qCreatedBy(id + "sun", EntityType.FACE), parallelPlane)),
                    "offsetDistance" : -definition.backlashVal
                });
    }
}


function getSunRotationMap(context is Context, id is Id, definition, sunDef)
{
    if (definition.numPlanetTeeth % 2 == 0)
    {
        definition.needsRotation = true;
    }
    else
    {
        definition.needsRotation = false;
    }

    const rotAngleEven = { "offsetAngle" : 180 / definition.numSunTeeth * degree, "offset" : true };
    const rotAngleOdd = { "offsetAngle" : 0 * degree, "offset" : true };



    if (definition.needsRotation)
    {
        return mergeMaps(sunDef, rotAngleEven);
    }
    else
    {
        return mergeMaps(sunDef, rotAngleOdd);
    }
}


function checkDefinition(context is Context, id is Id, definition)
{
    if ((definition.numSunTeeth % definition.numPlanets != 0) || (definition.numRingTeeth % definition.numPlanets != 0))
    {
        throw regenError("The number of planets must divide then Sun teeth and Ring teeth evenly");
    }

    if ((definition.numRingTeeth - definition.numSunTeeth) % 2 != 0)
    {
        if (definition.numSunTeeth % 2 == 0)
        {
            throw regenError("Number of Ring Teeth must be even");
        }
        else
        {
            throw regenError("Number of Ring Teeth must be odd");
        }
    }
    else if ((definition.numRingTeeth - definition.numSunTeeth) / 2 <= 3)
    {
        throw regenError("Not Enough Planet Teeth");
    }
}


export function editPlanetaryGearLogic(context is Context, id is Id, oldDefinition is map, definition is map, isCreating is boolean, specifiedParameters is map, hiddenBodies is Query) returns map
{
    // isCreating is required in the function definition for edit logic to work when editing an existing feature
    if (oldDefinition.numSunTeeth != definition.numSunTeeth)
    {
        definition.numRingTeeth = 2 * definition.numPlanetTeeth + definition.numSunTeeth;
        definition.module = definition.pitchCircleDiameter / definition.numSunTeeth;
        definition.circularPitch = definition.module * PI;
        definition.diametralPitch = 1 * inch / definition.module;
        return definition;
    }

    if (oldDefinition.numRingTeeth != definition.numRingTeeth)
    {

        definition.numPlanetTeeth = round((definition.numRingTeeth - definition.numSunTeeth) / 2);
        return definition;
    }

    if (oldDefinition.numPlanetTeeth != definition.numPlanetTeeth)
    {
        definition.numRingTeeth = round(2 * definition.numPlanetTeeth + definition.numSunTeeth);


        return definition;
    }

    if (oldDefinition.circularPitch != definition.circularPitch)
    {
        definition.module = definition.circularPitch / PI;
        definition.pitchCircleDiameter = (definition.circularPitch * definition.numTeeth) / PI;
        definition.diametralPitch = 1 * inch / definition.module;
        return definition;
    }

    if (oldDefinition.pitchCircleDiameter != definition.pitchCircleDiameter)
    {
        definition.module = definition.pitchCircleDiameter / definition.numSunTeeth;
        definition.circularPitch = (PI * definition.pitchCircleDiameter) / definition.numSunTeeth;
        definition.diametralPitch = 1 * inch / definition.module;
        return definition;
    }

    if (oldDefinition.module != definition.module)
    {
        definition.circularPitch = definition.module * PI;
        definition.pitchCircleDiameter = definition.numSunTeeth * definition.module;
        definition.diametralPitch = 1 * inch / definition.module;
        return definition;
    }

    if (oldDefinition.diametralPitch != definition.diametralPitch)
    {
        definition.circularPitch = PI / (definition.diametralPitch / inch);
        definition.module = definition.circularPitch / PI;
        definition.pitchCircleDiameter = (definition.circularPitch * definition.numSunTeeth) / PI;
        return definition;
    }

    return definition;
}

const SUN_TEETH_BOUNDS =
{
            (unitless) : [4, 12, 250]
        } as IntegerBoundSpec;

const PLANET_TEETH_BOUNDS =
{
            (unitless) : [4, 9, 250]
        } as IntegerBoundSpec;

const RING_TEETH_BOUNDS =
{
            (unitless) : [4, 30, 250]
        } as IntegerBoundSpec;

const PLANET_BOUNDS =
{
            (unitless) : [1, 1, 250]
        } as IntegerBoundSpec;

const PRESSURE_ANGLE_BOUNDS =
{
            (degree) : [12, 20, 35]
        } as AngleBoundSpec;

const MODULE_BOUNDS =
{
            (meter) : [1e-5, 0.001, 500],
            (centimeter) : 0.1,
            (millimeter) : 1.0,
            (inch) : 0.04
        } as LengthBoundSpec;

const CENTERHOLE_BOUNDS =
{
            (meter) : [1e-5, 0.01, 500],
            (centimeter) : 1.0,
            (millimeter) : 10.0,
            (inch) : 0.375
        } as LengthBoundSpec;

const KEY_BOUNDS =
{
            (meter) : [1e-5, 0.003, 500],
            (centimeter) : 0.3,
            (millimeter) : 3.0,
            (inch) : 0.125
        } as LengthBoundSpec;

const backlash_BOUNDS =
{
            (meter) : [-1, .00005, 1],
            (centimeter) : .005,
            (millimeter) : .05,
            (inch) : .002
        } as LengthBoundSpec;

