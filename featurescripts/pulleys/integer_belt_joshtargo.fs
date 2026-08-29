FeatureScript 2737;
import(path : "onshape/std/common.fs", version : "2737.0");
IconNamespace::import(path : "f9e7c05274a41bde699bf002", version : "25b7f144c02b506bf0609c81");


export enum PitchType
{
    annotation { "Name" : "Pitch: 0.25 inch" } PITCH250,
    annotation { "Name" : "Pitch: 5 mm" } PITCH5MM,
    annotation { "Name" : "Pitch: 3 mm" } PITCH3MM,
    annotation { "Name" : "Pitch: Custom" } CUSTOM
}

export enum Keep
{
    annotation { "Name" : "Keep Location 1" } KEEP1,
    annotation { "Name" : "Keep Location 2" } KEEP2
}

export enum AxisType
{
    annotation { "Name" : "Axis: C-C Line" } CC,
    annotation { "Name" : "Axis: Custom" } CUSTOM
}

annotation 
{ 
    "Feature Type Name" : "Integer Belt", 
    "Feature Type Description" : "Calculates and draws belt/chain loops of with correct dimensions for an integer number of teeth. Select points or circles for pullies, override pulley teeth, and choose axis", 
    "Icon" : IconNamespace::BLOB_DATA, 
    "Editing Logic Function" : "onFeatureChange" 
}

export const IntegerBelt = defineFeature(function(context is Context, id is Id, definition is map)
precondition
{
    annotation { "Name" : "Pitch" }
    definition.pitchType is PitchType;
        
    if(definition.pitchType == PitchType.CUSTOM)
    {
        annotation { "Name" : "Custom Pitch" }
        isLength(definition.pitch, LENGTH_BOUNDS);
    }    
    
    annotation { "Name" : "Location to Keep" }
    definition.keep is Keep;
    
    annotation { "Name" : "Axis Type" }
    definition.axisType is AxisType;
    
    if(definition.axisType == AxisType.CUSTOM)
    {
        annotation { "Name" : "Custom Axis", "Filter" : QueryFilterCompound.ALLOWS_AXIS, "MaxNumberOfPicks" : 1 }
        definition.axis is Query;
    }    
    
    annotation { "Name" : "+/- Teeth/Links" }
    isInteger(definition.adjust, {(unitless) : [-100,0,100]} as IntegerBoundSpec);
    
    //-----------------//
    
    annotation 
    { 
        "Name" : "Sketch Edge or Sketch Point", 
        "Filter" : (EntityType.VERTEX && SketchObject.YES) || (EntityType.EDGE && SketchObject.YES), 
        "MaxNumberOfPicks" : 2 
    }
    definition.picks is Query;
    
    annotation { "Name" : "Ovverride Tooth Count 1", "Default" : false }
    definition.override1 is boolean;

    if(definition.override1)
    {
        annotation { "Name" : "Tooth 1 Count" }
        isInteger(definition.tooth1, {(unitless) : [6,12,1000]} as IntegerBoundSpec);
    }
    
    annotation { "Name" : "Ovverride Tooth Count 2", "Default" : false }
    definition.override2 is boolean;

    if(definition.override2)
    {
        annotation { "Name" : "Tooth 2 Count" }
        isInteger(definition.tooth2, {(unitless) : [6,12,1000]} as IntegerBoundSpec);
    }
}
{
    const picksA = evaluateQuery(context, definition.picks);
    const keep = definition.keep == Keep.KEEP1 ? 0 : 1;
    const move = definition.keep == Keep.KEEP1 ? 1 : 0;
    
/// extract all information about the plane

    var sp = {};
        sp.plane = evOwnerSketchPlane(context, {"entity" : picksA[0]});
        sp.normal = sp.plane.normal;
        sp.x = sp.plane.x;
        sp.y = cross(sp.normal, sp.x);
        sp.origin = sp.plane.origin;
        
/// set pitch as a real number

    var pitch = 0.25 * inch;
        if(definition.pitchType == PitchType.PITCH5MM) pitch = 5 * millimeter; 
        else if(definition.pitchType == PitchType.PITCH3MM) pitch = 3 * millimeter; 
        else if(definition.pitchType == PitchType.CUSTOM) pitch = definition.pitch; 

/// calculate all information abou the points and dimensions for the pulleys or sprockets
    
    var p = {};
        p.query = makeArray(2);
        p.edge = [true, true];
        p.over = [definition.override1, definition.override2];
        p.tooth = [definition.tooth1, definition.tooth2];
        p.center = makeArray(2);
        p.dia = makeArray(2);

        for (var i = 0; i < size(picksA); i += 1)
        {
            const pick = picksA[i];
            p.query[i] = pick;

            if(isQueryEmpty(context, qEntityFilter(pick, EntityType.EDGE)))
            {
                p.edge[i] = false;
                p.center[i] = evVertexPoint(context, {"vertex" : pick});
                p.dia[i] = pitch * p.tooth[i] / PI;
            }
            else
            {
                const evcd = evCurveDefinition(context, {"edge" : pick});
                p.center[i] = evcd.coordSystem.origin;
                p.dia[i] = evcd.radius * 2;
    
                if(p.over[i]) {p.dia[i] = pitch * p.tooth[i] / PI;}
            }
            addDebugPoint(context, p.center[i]);
        }
        addDebugLine(context, p.center[0], p.center[1], DebugColor.RED);

/// initialize the center to center direction, distance, and normal

    var cc = {};
        cc.dir = normalize(p.center[1] - p.center[0]);
        cc.dist = norm(p.center[1] - p.center[0]);
        cc.normal = -cross(cc.dir, sp.normal);

/// calculate the length of the belt and the fractional number of teeth
        
    var length = 2 * cc.dist + (PI/2) * (p.dia[0] + p.dia[1]) + ((p.dia[0] - p.dia[1])^2)/(4 * cc.dist);
    
    var teethCalc = length/pitch;

/// calculate the adjusted belt info for the adjusted, integer number of teeth
                
    var adjust = {};
        adjust.count = definition.adjust;
        adjust.dir = adjust.count > 0 ? 1 : -1;
        adjust.teeth = teethCalc;
        adjust.length = pitch * adjust.teeth;
        adjust.cc = cc.dist;
        if(adjust.count != 0)
        {
            
            var adjustment = abs(adjust.count) == 1 ? 0 : adjust.count - adjust.dir;
            adjust.teeth = adjust.dir > 0 ? ceil(teethCalc) + adjustment : floor(teethCalc) + adjustment; 
            adjust.length = pitch * adjust.teeth;
            adjust.cc = (adjust.length - PI * (p.dia[0] + p.dia[1]) / 2) / 2;
        }

/// calculate the axis along which the non-keep centerpoint will move
        
    var axis = {};
        axis.direction = cc.dir;
        axis.origin = p.center[0];
        if(definition.axisType == AxisType.CUSTOM)
        {
            axis.direction = extractDirection(context, definition.axis);
        }
        if(definition.keep == Keep.KEEP1)
        {
            axis.origin = p.center[1];
        }
        axis.start = axis.origin - (cc.dist * axis.direction);
        axis.end = axis.origin + (cc.dist * axis.direction);

/// adjust the non-keep point along the axis

    const OGpoint = p.center[move];
    p.center[move] = pointsOnAxis(context, id, p.center[keep], p.center[move], axis.direction, adjust.cc);

    if(adjust.count != 0)
    {
        const arrowDirection = normalize(p.center[move] - OGpoint) * adjust.dir;
        addDebugArrow(context, p.center[move], p.center[move]+(cc.dist/2) * arrowDirection * adjust.dir, cc.dist/20, DebugColor.BLUE);
        addDebugLine(context, p.center[keep], p.center[move], DebugColor.BLUE);
    }
    
/// create all points needed for the sketch, using the moved point
    
    cc.dir = normalize(p.center[1] - p.center[0]);
    cc.dist = norm(p.center[1] - p.center[0]);
    cc.normal = -cross(cc.dir, sp.normal);

    var arc = {};
        arc.cen = makeArray(2);
        arc.start = makeArray(2);
        arc.end = makeArray(2);
        arc.midNeg = makeArray(2);
        arc.midPos = makeArray(2);
    
        for (var i = 0; i < 2; i += 1)
        {
            arc.cen[i] =    worldToPlane(sp.plane, p.center[i]);
            arc.start[i] =  worldToPlane(sp.plane, p.center[i] + (p.dia[i] / 2) * cc.normal);
            arc.end[i] =    worldToPlane(sp.plane, p.center[i] - (p.dia[i] / 2) * cc.normal);
            arc.midNeg[i] = worldToPlane(sp.plane, p.center[i] - (p.dia[i] / 2) * cc.dir);
            arc.midPos[i] = worldToPlane(sp.plane, p.center[i] + (p.dia[i] / 2) * cc.dir);
        }
    
/// make the ketch, the point, and a named curve    
        
    var sketchName = "sketch";
        makeLoopFromPoints(context, id, arc, axis, sp.plane, sketchName);
    
    var pointName = "point";
        opPoint(context, id + pointName, {"point" : p.center[move]});

    var extractName = "extract";
        opExtractWires(context, id + extractName, {"edges" : qCreatedBy(id + sketchName, EntityType.EDGE)});        

        if(adjust.count == 0) adjust.teeth = roundToPrecision(teethCalc, 3);

        var p1t = roundToPrecision(p.dia[0] * PI / pitch, 3);
        var p2t = roundToPrecision(p.dia[1] * PI / pitch, 3);

        var curveName = p1t~"T x "~p2t~"T x "~adjust.teeth~"T";

        setProperty(context, {
            "entities" : qCreatedBy(id + extractName, EntityType.BODY),
            "propertyType" : PropertyType.NAME,
            "value" : curveName
        });
});

export function onFeatureChange(context is Context, id is Id, oldDefinition is map, definition is map,
                                isCreating is boolean, specifiedParameters is map, hiddenBodies is Query) returns map
{
    var pickedEntities = evaluateQuery(context, definition.picks);

    if (size(pickedEntities) > 0 && !isQueryEmpty(context, qEntityFilter(pickedEntities[0], EntityType.VERTEX)))
    {
        definition.override1 = true;
    }

    if (size(pickedEntities) > 1 && !isQueryEmpty(context, qEntityFilter(pickedEntities[1], EntityType.VERTEX)))
    {
        definition.override2 = true;
    }

    return definition;
}


export function makeLoopFromPoints(context is Context, id is Id, arc, axis, sketchPlane, name)
{
    var sk = newSketchOnPlane(context, id + name, {"sketchPlane" : sketchPlane});

        axis.start = worldToPlane(sketchPlane, axis.start);
        axis.end = worldToPlane(sketchPlane, axis.end);

        skArc(sk, "arc0", 
        {
            "start" : arc.start[0], "mid" : arc.midNeg[0], "end" : arc.end[0], "construction" : true
        });
        
        skArc(sk, "arc1", 
        {
            "start" : arc.start[1], "mid" : arc.midPos[1], "end" : arc.end[1], "construction" : true
        });
    
        skLineSegment(sk, "topLine", { "start" : arc.start[0], "end" : arc.start[1], "construction" : true});
        skLineSegment(sk, "botLine", { "start" : arc.end[0], "end" : arc.end[1], "construction" : true});
    
        skConstraint(sk, "constraint1", {"constraintType" : ConstraintType.FIX, "localFirst" : "arc0"});
        skConstraint(sk, "constraint2", {"constraintType" : ConstraintType.FIX, "localFirst" : "arc1"});
    
        skConstraint(sk, "constraint7", {
            "constraintType" : ConstraintType.COINCIDENT, "localFirst" : "topLine.start", "localSecond" : "arc0.start"});
        skConstraint(sk, "constraint8", {
            "constraintType" : ConstraintType.COINCIDENT, "localFirst" : "topLine.end", "localSecond" : "arc1.end"});
        skConstraint(sk, "constraint9", {
            "constraintType" : ConstraintType.COINCIDENT, "localFirst" : "botLine.start", "localSecond" : "arc0.end"});
        skConstraint(sk, "constraint10", {
            "constraintType" : ConstraintType.COINCIDENT, "localFirst" : "botLine.end", "localSecond" : "arc1.start"});
    
        skConstraint(sk, "constraint3", {
            "constraintType" : ConstraintType.TANGENT, "localFirst" : "arc0", "localSecond" : "topLine"});
        skConstraint(sk, "constraint4", {
            "constraintType" : ConstraintType.TANGENT, "localFirst" : "arc1", "localSecond" : "topLine"});
        skConstraint(sk, "constraint5", {
            "constraintType" : ConstraintType.TANGENT, "localFirst" : "arc0", "localSecond" : "botLine"});
        skConstraint(sk, "constraint6", {
            "constraintType" : ConstraintType.TANGENT, "localFirst" : "arc1", "localSecond" : "botLine"});
            
    skSolve(sk);
}

export function pointsOnAxis(context is Context, id is Id, center is Vector, axisPoint is Vector, axisDir is Vector, distance is ValueWithUnits) returns Vector
{
    var denom = sqrt(dot(axisDir, axisDir));
    if (denom == 0*meter)
        throw regenError("axisDir must be nonzero.", []);
    var u = axisDir / denom; // unit direction (unitless)

    var w  = center - axisPoint;            // vector from axis to center
    var b  = dot(u, w);                     // projection length along axis
    var d2 = dot(w, w) - b*b;               // squared perpendicular distance
    var L2 = distance * distance;

    if (L2 < d2 - 1e-18*meter*meter)
    {
        reportFeatureWarning(context, id, "Distance too small for chosen Axis");
    }

    var s  = sqrt(max(0*meter*meter, L2 - d2)); // offset along axis from foot
    var t1 = b - s;
    var t2 = b + s;

    var tClosest = (abs(t1) <= abs(t2)) ? t1 : t2;
    var point = axisPoint + tClosest * u;

    return point;
}
