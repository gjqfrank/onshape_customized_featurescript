FeatureScript 608;
import(path : "onshape/std/geometry.fs", version : "608.0");

export import(path : "5742c8cde4b06c68b362d748/af6c7087f93a916dda2efe84/01a666571e625f8b819fd75b", version : "ff892a8c8a7324eaa805370a");

annotation { "Feature Type Name" : "Drive Train Generator", "Editing Logic Function" : "editDriveTrainGearLogic" }
export const driveTrainGen = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "NeedsRotation", "UIHint" : "ALWAYS_HIDDEN", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        definition.needsRotation is boolean;

        annotation { "Name" : "Select Points", "Filter" : EntityType.VERTEX, "MaxNumberOfPicks" : 100 }
        definition.sketchPoints is Query;

        annotation { "Name" : "Reduction Ratio Desired" }
        isReal(definition.reductionRatio, RATIO_BOUNDS);

        annotation { "Name" : "Run Reduction Optimization" }
        definition.runOptimization is boolean;

        annotation { "Name" : "Minimum Number of teeth" }
        isInteger(definition.minTeeth, BASE_TEETH_BOUNDS);

        annotation { "Name" : "Output Shaft Number" }
        isInteger(definition.outputShaftNumber, POSITIVE_COUNT_BOUNDS);
        
        annotation { "Name" : "Gear Spacing" }
        definition.gearSpacingBool is boolean;
        
        if (definition.gearSpacingBool)
        {
            annotation { "Name" : "Spacing Type" }
            definition.spacingType is SPACING_TYPE;
            
            annotation { "Name" : "Gear Spacing" }
            isLength(definition.gearSpacing, GEAR_SPACING_BOUNDS);
        }
        

        annotation { "Name" : "Generate Bodies", "Default" : true }
        definition.generateBodies is boolean;

        annotation { "Name" : "Input type" }
        definition.GearInputType is GearInputType;

        if (definition.GearInputType == GearInputType.module)
        {
            annotation { "Name" : "Module Desired" }
            isLength(definition.module, MODULE_BOUNDS);
        }

        if (definition.GearInputType == GearInputType.diametralPitch)
        {
            annotation { "Name" : "Diametral Pitch Desired" }
            isReal(definition.diametralPitch, POSITIVE_REAL_BOUNDS);
        }

        if (definition.GearInputType == GearInputType.circularPitch)
        {
            annotation { "Name" : "Circular Pitch Desired" }
            isLength(definition.circularPitch, LENGTH_BOUNDS);
        }

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


        annotation { "Name" : "Extrude depth", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        isLength(definition.gearDepth, BLEND_BOUNDS);

        annotation { "Name" : "Extrude direction", "UIHint" : "OPPOSITE_DIRECTION", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        definition.flipGear is boolean;

        annotation { "Name" : "Offset", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
        definition.offset is boolean;

        if (definition.offset)
        {
            annotation { "Name" : "Backlash Value", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isLength(definition.backlashVal, backlash_BOUNDS);

            annotation { "Name" : "Root diameter", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isLength(definition.offsetClearance, ZERO_DEFAULT_LENGTH_BOUNDS);

            annotation { "Name" : "Dedendum factor", "Default" : DedendumFactor.d250, "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            definition.dedendumFactor is DedendumFactor;

            annotation { "Name" : "Outside diameter", "UIHint" : "REMEMBER_PREVIOUS_VALUE" }
            isLength(definition.offsetDiameter, ZERO_DEFAULT_LENGTH_BOUNDS);
        }
    }

    {
        const errorTol = 0.1; //The error in final reduction ratio that is allowed (this is for a single pair of gears, not the overall system) so that the reduction is considered finished.
        var currentReduction = 1;
        var pairReduction = 1; //Reduction for a given pair of gears. Used to keep track of reduction ratios.
        var reductionTrackingNew = [[currentReduction, pairReduction, definition.reductionRatio, 0]]; //Defines a basis for the reduction tracking array. This is used to evaluate reductions after they're generated.
        var reductionTracking = reductionTrackingNew;
        var sketchPointsList = evaluateQuery(context, definition.sketchPoints);
        
        if (!definition.gearSpacingBool)
        {
            definition.gearSpacing = 0 * inch;
        }

        
        //This is the definition for a gear that is passed into the functions that create the gears. It's used by spurGear in order to specify all of the information.
        var gearSpecifics = {
            "GearInputType" : GearInputType.module,
            "center" : definition.center,
            "centerHole" : definition.centerHole,
            "centerHoleDia" : definition.centerHoleDia,
            "centerPoint" : false,
            "circularPitch" : definition.circularPitch,
            "dedendumFactor" : definition.dedendumFactor,
            "diametralPitch" : definition.diametricalPitch,
            "flipGear" : definition.flipGear,
            "gearDepth" : definition.gearDepth,
            "key" : definition.key,
            "keyHeight" : definition.keyHeight,
            "keyWidth" : definition.keyWidth,
            "module" : definition.module,
            "offset" : definition.offset,
            "offsetAngle" : 0 * degree,
            "offsetClearance" : definition.offsetClearance,
            "offsetDiameter" : definition.offsetDiameter,
            "pressureAngle" : definition.pressureAngle,
            "rootFillet" : definition.rootFillet,
            "backlash" : definition.backlashVal,
            "chamfer" : false,
            "helical" : false,
        };

        //Creates a one-hot vector (which is easy to reverse when the gears move back and forth across the axles) to represent the output shaft.
        definition.outputShaftNumber = convertToOneHot(definition.outputShaftNumber, size(sketchPointsList));

        var createBodiesDefault = false;
        if (definition.generateBodies && !definition.runOptimization)
        {
            createBodiesDefault = definition.generateBodies;
        }

        //Run the standard gear generator. If optimization is off and generateBodies is true, then generate bodies.
        reductionTracking = createGearLayers(context, id, gearSpecifics, sketchPointsList, reductionTracking, definition.reductionRatio, definition.minTeeth, errorTol, 0 * inch, 0, definition.outputShaftNumber, createBodiesDefault, false, false, definition.gearSpacing, definition.spacingType);


        if (definition.runOptimization)
        {
            //Defines the necessary information the run optimization on the gear layers generated the previous call of createGearLayers
            var gearLayerSpecifics = { "sketchPointsList" : sketchPointsList, "gearSpecifics" : gearSpecifics, "definition" : definition, "errorTol" : errorTol };

            var optimizedTracking = runGearOptimization(context, id, reductionTracking, definition.reductionRatio, definition.minTeeth, gearLayerSpecifics);

            reductionTracking = createGearLayers(context, id, gearSpecifics, sketchPointsList, reductionTrackingNew, definition.reductionRatio, definition.minTeeth, errorTol, 0 * inch, 0, definition.outputShaftNumber, definition.generateBodies, optimizedTracking, true, definition.gearSpacing, definition.spacingType);
        }
       
        var outputString = displayCorrectDistancesByModule(context, id, sketchPointsList, reductionTracking, gearSpecifics.module);
        outputString = "Actual Reduction Ratio is: " ~ floatToPrecision(evaluateReductionRatios(reductionTracking, definition.reductionRatio), 6) ~ "<br>" ~ outputString;
        reportFeatureInfo(context, id, outputString);
    });

function runGearOptimization(context is Context, id is Id, reductionTracking, reductionRatio, minNumTeeth, gearLayerSpecifics is map)
{
    var reductionTrackingNew = [[1, 1, 0, 0]];
    var sketchPointsList = gearLayerSpecifics.sketchPointsList;
    var gearSpecifics = gearLayerSpecifics.gearSpecifics;
    var definition = gearLayerSpecifics.definition;
    var errorTol = gearLayerSpecifics.errorTol;
    var returnedReductionTracking = [];

    var pastBest = reductionTracking;
    var result = [];

    var gearTeethModifications = makeArray(size(reductionTracking));
    for (var elementNumber = 0; elementNumber < size(reductionTracking); elementNumber += 1)
    {
        gearTeethModifications[elementNumber] = [0, 0, 0, 0];
    }

    for (var i = 0; i < size(reductionTracking); i += 1)
    {
        for (var j = 0; j < abs(reductionTracking[i][2] - reductionTracking[i][3]); j += 1)
        {

            if (reductionTracking[i][2] + gearTeethModifications[i][2] + 1 < reductionTracking[i][3] + gearTeethModifications[i][3])
            {
                gearTeethModifications[i][2] += 1;
                gearTeethModifications[i][3] -= 1;
            }
            else if (reductionTracking[i][2] + gearTeethModifications[i][2] - 1 > reductionTracking[i][3] + gearTeethModifications[i][3])
            {
                gearTeethModifications[i][2] -= 1;
                gearTeethModifications[i][3] += 1;
            }

            returnedReductionTracking = createGearLayers(context, id, gearSpecifics, sketchPointsList, reductionTrackingNew, definition.reductionRatio, definition.minTeeth, errorTol, 0 * inch, 0, definition.outputShaftNumber, false, gearTeethModifications, false, 0 * inch, SPACING_TYPE.MID);

            if (abs(returnedReductionTracking[size(returnedReductionTracking) - 1][0] - reductionRatio) < abs(pastBest[size(pastBest) - 1][0] - reductionRatio))
            {
                pastBest = returnedReductionTracking;
                result = gearTeethModifications;
            }
        }
    }
    return result;
}


function displayCorrectDistancesByModule(context is Context, id is Id, sketchPointsList, reductionTracking, attemptedModule)
{
    var outputString = "";
    outputString = "Attempted Module is " ~ floatToPrecision(attemptedModule.value * 39.3701, 6) ~ " in. <br>";
    for (var i = 1; i < size(sketchPointsList); i += 1)
    {
        var distance = evDistance(context, {
                    "side0" : sketchPointsList[i - 1],
                    "side1" : sketchPointsList[i]
                })['distance'];

        var sumNumTeeth = round(2 * distance / attemptedModule);


        var realModule = getRealModule(distance, sumNumTeeth);

        var goodGearSpacing = sumNumTeeth * attemptedModule;

        outputString = outputString ~ "Real Module from shafts " ~ i ~ " to " ~ i + 1 ~ " is " ~ floatToPrecision(realModule.value * 39.3701, 6) ~ " in. Spacing by " ~ floatToPrecision(goodGearSpacing.value * 39.3701, 6) ~ " in. might be a good idea. <br>";


    }

    outputString = outputString ~ "Make the spacing of your shafts an integer multiple of " ~ floatToPrecision(attemptedModule.value * 39.3701, 6) ~ " in. to get the exact module for your gears" ~ "<br>";

    return outputString;
}

function floatToPrecision(float, digitsDisplayed)
{
    var stringArray = splitIntoCharacters(toString(float));
    var resultString = "";
    for (var i = 0; i < size(stringArray); i += 1)
    {
        resultString = resultString ~ stringArray[i];

        if (stringArray[i] == ".")
        {
            for (var j = 1; j < digitsDisplayed + 1; j += 1)
            {
                if ((j+i) > size(stringArray)-1)
                {
                    resultString = resultString ~ "0";
                }else
                {
                    resultString = resultString ~ stringArray[j + i];
                }
                
            }
            return resultString;
        }

    }

    return resultString;
}

function evaluateReductionRatios(reductionTracking, reductionRatio)
{
    var currentRatio = 1;
    var singleReductionRatio = 1;

    for (var i = 1; i < size(reductionTracking); i += 1)
    {
        singleReductionRatio = reductionTracking[i][3] / reductionTracking[i][2];

        currentRatio = currentRatio * singleReductionRatio;
    }

    return currentRatio;
}

function convertToOneHot(numberToConvert, sizeOfVector) //Start at 0 for numberToConvert
{

    var result = zeroVector(sizeOfVector);
    if (numberToConvert > sizeOfVector)
    {
        throw regenError("Output Shaft Number should be less than or equal to the total number of points selected");
    }
    result[numberToConvert - 1] = 1;
    return result;
}


function createGearLayers(context is Context, id is Id, gearSpecifics, sketchPointsList, reductionTracking, reductionRatio, minTeeth, errorTol, offsetSize, recursionDepth, outputShaftNumber, createBodies is boolean, overrideReduction, overrideTeeth is boolean, gearSpacing is ValueWithUnits, spacingType is SPACING_TYPE)
{

    if (recursionDepth > 10)
    {
        throw regenError("Maximum Recursion Depth Reached");
    }
    recursionDepth += 1;
    var reductionRatioErrorVal = 0;

    for (var i = 1; i < size(sketchPointsList); i += 1)
    {
        var xAxis is Vector = evVertexPoint(context, {
                    "vertex" : sketchPointsList[i]
                }) - evVertexPoint(context, {
                    "vertex" : sketchPointsList[i - 1]
                });
        var previousPoint = sketchPointToCoord(context, sketchPointsList[i - 1], xAxis);
        var currentPoint = sketchPointToCoord(context, sketchPointsList[i], xAxis);
        
        var increaseThicknessInfo = {"distancePrimary" : gearSpacing, "distanceSecondary" : 0 * inch,  "gearNum" : 0};
        var offsetSpacing = (gearSpacing) * (i - 2);
        var additionalSpacing = gearSpacing;
        if (i == 1 && recursionDepth == 1)
        {
            offsetSpacing = 0 * inch;
            increaseThicknessInfo.distancePrimary = 0 * inch;
            additionalSpacing = 0 * inch;
        }
        
        
        var gearOffsets = [offsetSpacing + (gearSpecifics.gearDepth) * (i - 1)+ offsetSize, (gearSpacing + gearSpecifics.gearDepth) * (i - 1) + offsetSize];
        if (spacingType == SPACING_TYPE.SECONDARY)
        {
            gearOffsets[1] = offsetSpacing + (gearSpecifics.gearDepth) * (i - 1)+ offsetSize + additionalSpacing;
            gearOffsets[0] += additionalSpacing;
            increaseThicknessInfo.distanceSecondary = increaseThicknessInfo.distancePrimary;
            increaseThicknessInfo.distancePrimary = 0 * inch;
            
            if (i == 1 && recursionDepth == 1)
            {
                increaseThicknessInfo.distanceSecondary = gearSpacing;
            }

        }

        //Creates coordinate systems that are offset from the previous gears.
        var liftedPoint1 = makeCoordVerticalCopy(context, id + ("L_1_G_" ~ i), previousPoint, gearOffsets[0]);
        var liftedPoint2 = makeCoordVerticalCopy(context, id + ("L_1_G_" ~ i), currentPoint, gearOffsets[1]);
        var originPoints2 = [liftedPoint1, liftedPoint2];

        var newBaseTeeth = 0;

        //Generate bodies logic for the default (non-optimized version). Is true if optimization is false and createBodies is true.
        var defaultGenerateBodies = false;
        if (createBodies && !overrideTeeth)
        {
            defaultGenerateBodies = createBodies;
        }

        reductionTracking = append(reductionTracking, makeGearPair(context, id + ("L_1_" ~ i), originPoints2, gearSpecifics, reductionRatio, reductionTracking[size(reductionTracking) - 1][0], minTeeth, defaultGenerateBodies, false, newBaseTeeth, increaseThicknessInfo));

        var currentReductionTemp = 1;
        var pastReductionTemp = 1;
        if (overrideReduction != false && (size(reductionTracking) <= size(overrideReduction)))
        {

            currentReductionTemp = (overrideReduction[size(reductionTracking) - 1][3] + reductionTracking[size(reductionTracking) - 1][3]) / (overrideReduction[size(reductionTracking) - 1][2] + reductionTracking[size(reductionTracking) - 1][2]);
            pastReductionTemp = reductionTracking[size(reductionTracking) - 1][3] / reductionTracking[size(reductionTracking) - 1][2];

            reductionTracking[size(reductionTracking) - 1][3] = overrideReduction[size(reductionTracking) - 1][3] + reductionTracking[size(reductionTracking) - 1][3];
            reductionTracking[size(reductionTracking) - 1][2] = overrideReduction[size(reductionTracking) - 1][2] + reductionTracking[size(reductionTracking) - 1][2];

            //Assign the pair reduction ratio based on the new ratio
            reductionTracking[size(reductionTracking) - 1][1] = currentReductionTemp;

            //Reassiagn Current Reduction to include the new gear ratio
            reductionTracking[size(reductionTracking) - 1][0] = reductionTracking[size(reductionTracking) - 1][0] * currentReductionTemp / pastReductionTemp;
        }

        //If overrideTeeth is true, then optimization is on (thus an override is being placed on the teeth) If this is true, then, createBodies is what determines whether bodies are created or not.
        if (overrideTeeth)
        {
            makeGearPair(context, id + ("L_R_" ~ i), originPoints2, gearSpecifics, reductionRatio, reductionTracking[size(reductionTracking) - 1][0], minTeeth, createBodies, true, reductionTracking[size(reductionTracking) - 1][2], increaseThicknessInfo);
        }

        reductionRatio = reductionRatio / reductionTracking[size(reductionTracking) - 1][1];

        reductionRatioErrorVal = invertError(reductionRatio);

        if ((abs(reductionRatioErrorVal) < (1 + errorTol)) && (outputShaftNumber[i] == 1))
        {
            return reductionTracking;
        }
    }

    if ((abs(reductionRatioErrorVal) > 1 + errorTol) || (outputShaftNumber[size(sketchPointsList) - 1] != 1))
    {
        sketchPointsList = reverse(sketchPointsList);
        offsetSize = offsetSize + (size(sketchPointsList) - 1) * (gearSpecifics.gearDepth + gearSpacing);
        outputShaftNumber = reverse(outputShaftNumber);
        reductionTracking = createGearLayers(context, id + ("recursionDepth_" ~ recursionDepth), gearSpecifics, sketchPointsList, reductionTracking, reductionRatio, minTeeth, errorTol, offsetSize, recursionDepth, outputShaftNumber, createBodies, overrideReduction, overrideTeeth, gearSpacing, spacingType);
    }

    return reductionTracking;
}


function invertError(reductionRatio)
{
    var reductionRatioErrorVal = 0;

    if (reductionRatio > 1)
    {
        reductionRatioErrorVal = reductionRatio;
    }
    else
    {
        reductionRatioErrorVal = 1 / reductionRatio;
    }

    return reductionRatioErrorVal;
}

function makeCoordVerticalCopy(context is Context, id is Id, coordSys, thickness)
{
    return coordSystem(coordSys.origin + coordSys.zAxis * thickness, coordSys.xAxis, coordSys.zAxis);
}

function sketchPointToCoord(context, vertex, xAxis)
{
    return coordSystem(evVertexPoint(context, {
                    "vertex" : vertex
                }), xAxis, evOwnerSketchPlane(context, {
                        "entity" : vertex
                    }).normal);
}

function getRealModule(distance, sumNumTeeth)
{
    return 2 * distance / sumNumTeeth;
}


function makeGearPair(context is Context, id is Id, originPoints is array, gearSpecifics is map, pairReductRatio, currentReduction, minTeeth, createBodies is boolean, overrideTeethCounts is boolean, newBaseTeeth, increaseThicknessInfo is map)
{
    var distance = evDistance(context, {
                "side0" : originPoints[0].origin,
                "side1" : originPoints[1].origin
            })['distance'];

    var sumNumTeeth = round(2 * distance / gearSpecifics.module);
    pairReductRatio = approximateReductionRatio(sumNumTeeth, pairReductRatio);
    gearSpecifics.module = getRealModule(distance, sumNumTeeth);


    var baseNumTeeth = round(sumNumTeeth / (pairReductRatio + 1));
    if (baseNumTeeth == 0)
    {
        baseNumTeeth = 1;
    }

    var secondaryNumTeeth = round(baseNumTeeth * pairReductRatio);
    if (secondaryNumTeeth == 0)
    {
        secondaryNumTeeth = 1;
    }

    if (baseNumTeeth < minTeeth)
    {
        baseNumTeeth = minTeeth;
        secondaryNumTeeth = sumNumTeeth - baseNumTeeth;
        pairReductRatio = secondaryNumTeeth / (baseNumTeeth);

    }
    else if (secondaryNumTeeth < minTeeth)
    {
        secondaryNumTeeth = minTeeth;
        baseNumTeeth = sumNumTeeth - secondaryNumTeeth;
        pairReductRatio = secondaryNumTeeth / (baseNumTeeth);
    }

    if (overrideTeethCounts)
    {
        baseNumTeeth = newBaseTeeth;
        secondaryNumTeeth = sumNumTeeth - baseNumTeeth;
        pairReductRatio = secondaryNumTeeth / (baseNumTeeth);
    }

    // Define gear constraint equations for proper meshing given a reduction ratio
    var pitchCircle1 = 2 * distance / (1 + pairReductRatio);
    var pitchCircle2 = 2 * distance - pitchCircle1;

    if (createBodies)
    {
        var baseDefPrimary = {
            "numTeeth" : baseNumTeeth,
            "pitchCircleDiameter" : pitchCircle1,
            "gearDepth" : gearSpecifics.gearDepth + increaseThicknessInfo.distancePrimary,
        };

        SpurGear(context, id + "gear1", mergeMaps(gearSpecifics, baseDefPrimary));
        backlashGears(context, id + "backlash_1", gearSpecifics, id + "gear1");
        performTransform(context, id + "transform_1", originPoints[0], id + "gear1");

        var baseDefSecondary = {
            "numTeeth" : secondaryNumTeeth,
            "pitchCircleDiameter" : pitchCircle2,
            "gearDepth" : gearSpecifics.gearDepth + increaseThicknessInfo.distanceSecondary
        };

        SpurGear(context, id + "gear2", getGearRotationMap(context, id, mergeMaps(gearSpecifics, baseDefSecondary)));
        backlashGears(context, id + "backlash_2", gearSpecifics, id + "gear2");
        performTransform(context, id + "transform_2", originPoints[1], id + "gear2");
    }

    return [currentReduction * pairReductRatio, pairReductRatio, baseNumTeeth, secondaryNumTeeth];
}

function approximateReductionRatio(sumNumTeeth, currentReductionRatio)
{
    var numBaseTeeth = round(sumNumTeeth / (currentReductionRatio + 1));
    return sumNumTeeth / numBaseTeeth - 1;
}


function backlashGears(context is Context, id is Id, gearSpecifics, gearId is Id)
{
    var parallelPlane = evPlane(context, {
            "face" : qCreatedBy(makeId("Front"), EntityType.FACE)
        });

    // if (gearSpecifics.offset)
    // {
    //     opOffsetFace(context, id + "offsetFace1", {
    //                 "moveFaces" : qSubtraction(qCreatedBy(gearId, EntityType.FACE), qParallelPlanes(qCreatedBy(gearId, EntityType.FACE), parallelPlane)),
    //                 "offsetDistance" : gearSpecifics.backlashVal
    //             });

    // }
}

function getGearRotationMap(context is Context, id is Id, gearDef)
{
    const rotAngleEven = { "offsetAngle" : 180 / gearDef.numTeeth * degree, "offset" : true };
    const rotAngleOdd = { "offsetAngle" : 0 * degree, "offset" : true };


    if (gearDef.numTeeth % 2 == 0)
    {
        gearDef.needsRotation = true;
        return mergeMaps(gearDef, rotAngleEven);
    }
    else
    {
        gearDef.needsRotation = false;
        return mergeMaps(gearDef, rotAngleOdd);
    }

}

function performTransform(context is Context, id is Id, newCoordSys, gearId is Id)
{



    const rotTransform = rotationAround(line(vector(0, 0, 0) * inch, vector(-1, 0, 0) * inch), 90 * degree);
    const finalTransform = toWorld(newCoordSys) * rotTransform;

    var moveObjects = qCreatedBy(gearId, EntityType.BODY);

    opTransform(context, id + "transformFinal", {
                "bodies" : moveObjects,
                "transform" : finalTransform
            });
}




export function editDriveTrainGearLogic(context is Context, id is Id, oldDefinition is map, definition is map, isCreating is boolean, specifiedParameters is map, hiddenBodies is Query) returns map
{



    if (oldDefinition.module != definition.module)
    {
        definition.circularPitch = definition.module * PI;
        definition.diametralPitch = 1 * inch / definition.module;
        return definition;
    }

    if (oldDefinition.circularPitch != definition.circularPitch)
    {
        definition.module = definition.circularPitch / PI;
        definition.diametralPitch = 1 * inch / definition.module;
        return definition;
    }


    if (oldDefinition.diametralPitch != definition.diametralPitch)
    {
        definition.circularPitch = PI / (definition.diametralPitch / inch);
        definition.module = definition.circularPitch / PI;
        return definition;
    }

    return definition;
}

export enum SPACING_TYPE
{

    annotation { "Name" : "Secondary" }
    SECONDARY,
    annotation { "Name" : "Primary" }
    PRIMARY,
    annotation { "Name" : "Middle" }
    MID

}

const BASE_TEETH_BOUNDS =
{
            (unitless) : [4, 12, 250]
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

const GEAR_SPACING_BOUNDS =
{
            (meter) : [1e-5, 0.002, 500],
            (centimeter) : 0.2,
            (millimeter) : 1.0,
            (inch) : 0.05
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
            (meter) : [-1, -.00005, 1],
            (centimeter) : -.005,
            (millimeter) : -.05,
            (inch) : -.002
        } as LengthBoundSpec;

const RATIO_BOUNDS =
{
            (unitless) : [1e-5, .5, 1000000]
        } as RealBoundSpec;
