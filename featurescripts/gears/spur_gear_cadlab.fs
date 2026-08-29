FeatureScript 355;
import(path : "onshape/std/geometry.fs", version : "355.0");

annotation { "Feature Type Name" : "Summer Spur Gear", "Feature Name Template" : "Spur Gear (#teeth teeth)", "Filter Selector" : "fs", "Editing Logic Function" : "editGearLogic" }
export const SpurGear = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "teeth", "UIHint" : "ALWAYS_HIDDEN" }
        definition.teeth is string; //used to name the feature only

        annotation { "Name" : "Number of teeth" }
        isInteger(definition.numTeeth, TEETH_BOUNDS);

        annotation { "Name" : "Input type" }
        definition.GearInputType is GearInputType;

        if (definition.GearInputType == GearInputType.module)
        {
            annotation { "Name" : "Module" }
            isLength(definition.module, MODULE_BOUNDS);
        }

        if (definition.GearInputType == GearInputType.diametralPitch)
        {
            annotation { "Name" : "Diametral pitch" }
            isReal(definition.diametralPitch, POSITIVE_REAL_BOUNDS);
        }

        if (definition.GearInputType == GearInputType.circularPitch)
        {
            annotation { "Name" : "Circular pitch" }
            isLength(definition.circularPitch, LENGTH_BOUNDS);
        }

        annotation { "Name" : "Pitch circle diameter" }
        isLength(definition.pitchCircleDiameter, LENGTH_BOUNDS);

        annotation { "Name" : "Pressure angle" }
        isAngle(definition.pressureAngle, PRESSURE_ANGLE_BOUNDS);

        annotation { "Name" : "Center hole" }
        definition.centerHole is boolean;

        if (definition.centerHole)
        {
            annotation { "Name" : "Hole diameter" }
            isLength(definition.centerHoleDia, CENTERHOLE_BOUNDS);

            annotation { "Name" : "Keyway" }
            definition.key is boolean;

            if (definition.key)
            {
                annotation { "Name" : "Key width" }
                isLength(definition.keyWidth, KEY_BOUNDS);

                annotation { "Name" : "Key height" }
                isLength(definition.keyHeight, KEY_BOUNDS);
            }
        }

        annotation { "Name" : "Select origin position" }
        definition.centerPoint is boolean;

        if (definition.centerPoint)
        {
            annotation { "Name" : "Sketch vertex for center", "Filter" : EntityType.VERTEX && SketchObject.YES, "MaxNumberOfPicks" : 1 }
            definition.center is Query;
        }

        annotation { "Name" : "Extrude depth" }
        isLength(definition.gearDepth, BLEND_BOUNDS);

        annotation { "Name" : "Extrude direction", "UIHint" : "OPPOSITE_DIRECTION" }
        definition.flipGear is boolean;

        annotation { "Name" : "Offset" }
        definition.offset is boolean;

        if (definition.offset)
        {
            annotation { "Name" : "Root diameter" }
            isLength(definition.offsetClearance, ZERO_DEFAULT_LENGTH_BOUNDS);

            annotation { "Name" : "Outside diameter" }
            isLength(definition.offsetDiameter, ZERO_DEFAULT_LENGTH_BOUNDS);

            annotation { "Name" : "Tooth angle" }
            isAngle(definition.offsetAngle, ANGLE_360_ZERO_DEFAULT_BOUNDS);
        }
    }
    {
        var offsetDiameter = 0 * meter;
        var offsetClearance = 0 * meter;
        var offsetAngle = 0 * degree;

        if (definition.offset)
        {
            offsetDiameter = definition.offsetDiameter;
            offsetClearance = definition.offsetClearance;
            offsetAngle = definition.offsetAngle;
        }

        if (definition.centerHole && definition.centerHoleDia >= definition.pitchCircleDiameter - 4 * definition.module)
        {
            throw regenError("Center hole diameter must be less than the root diameter", ["centerHoleDia"]);
        }

        if (definition.key && definition.keyHeight / 2 + definition.centerHoleDia >= definition.pitchCircleDiameter - 4 * definition.module)
        {
            throw regenError("Center hole diameter plus Key height must be less than the root diameter", ["keyHeight"]);
        }
        // if no center vertex selected build gear on the front plane at the origin
        var location = vector(0, 0, 0) * meter;
        var sketchPlane = plane(location, vector(0, -1, 0), vector(1, 0, 0));
        var center3D = location;

        // else find location of selected vertex and its sketch plane and create a new sketch for the gear profile
        if (definition.centerPoint)
        {
            location = evaluateQuery(context, definition.center)[0];
            sketchPlane = evOwnerSketchPlane(context, { "entity" : location });
            center3D = evVertexPoint(context, { "vertex" : location });
        }

        const gearSketch = newSketchOnPlane(context, id + "gearSketch", { "sketchPlane" : sketchPlane });
        const center2D = worldToPlane(sketchPlane, center3D);

        var filletEdges = [];
        var regionPoint;
        // 漸開線近似點數
        var imax = 5;
        // 使用者所選的齒輪圓心 x 座標
        var midx = center2D[0];
        // 使用者所選的齒輪圓心 y 座標
        var midy = center2D[1];
        // 齒數
        var n = definition.numTeeth;
        // 模數
        var m = definition.module;
        // 壓力角, 單位為角度
        var pa = definition.pressureAngle;
        // 齒輪的節圓半徑
        var rp = m*n/2;

        // 正齒輪囓合用的定位線
        skLineSegment(gearSketch, "line", {
        "start" : vector(midx,midy),
        "end" : vector(midx,midy+rp)
        });

        // 齒根, 暫時不考慮納入 offsetClearance
        var d = 2.5*rp/n;
        // 齒頂圓半徑, 暫不考慮納入 offsetDiameter
        var ra = rp + m;
        // 基圓半徑
        var rb = rp*cos(pa);
        //print(rb);
        // 齒根圓半徑
        var rd = rp - d;
        // 分段後齒頂與齒根半徑差增量
        var dr = 0*meter;
        // 若 rb > rd 時從基圓開始繪製漸開線, 但是若 rd > rb, 則漸開線從 rd 畫到齒頂圓
        if (rd > rb)
        {
            // 半徑差的分段, 由齒根圓到齒頂圓
            dr = (ra-rd)/imax;
        }
        else
        {
            // 半徑差的分段, 由基圓到齒頂圓
            dr = (ra-rb)/imax;
        }
        // PI 為實數值沒有單位, tan(pa)也沒有單位, pa 已經設定單位為 degree
        var rot = PI/(2*n)*radian;
        // 用來設定 entity id 用的增量變數
        var nameId = 1;
        var r = 0*meter;
        // theta 為浮點數字
        var theta = 0;
        var inv = 0*radian;
        var inc = 0*radian;
        // 當 r=rp 時 ,計算 inv_rp 用來旋轉漸開線用
        // theta 為沒有單位的實數
        theta = sqrt((rp*rp)/(rb*rb)-1);
        // atan(theta) 為 radian
        // Onshape SG 的 const alpha 就是這裡的 inv_rp
        // Onshape SG 的 const beta 就是這裡的 rot-inv_rp
        var inv_rp = theta*radian-atan(theta);
        // 漸開線上點的 x 座標
        var xpt = 0*meter;
        // 漸開線上點的 y 座標
        var ypt = 0*meter;
        // 左側漸開線第1點座標 left first x and y
        var lfx = 0*meter;
        var lfy = 0*meter;
        // 右側漸開線第1點座標 right first x and y
        var rfx = 0*meter;
        var rfy = 0*meter;
        // 左側齒根圓上點座標 left x of dedendum point
        var lxd = 0*meter;
        var lyd = 0*meter;
        // 右側齒根圓上點座標 right x of dedendum point
        var rxd = 0*meter;
        var ryd = 0*meter;
        // 左側齒根圓上點座標 right x of dedendum point (advanced)
        var lxd_ad = 0*meter;
        var lyd_ad = 0*meter;
        var inc_ad = 0*radian;

        for (var j=0;j<n;j+=1)
        {
            var involute1 = [];
            var involute2 = [];
            var arcDone = false;
            var point1;
            var point2;

            // 當 j 增量時, 按照齒數輪廓繞行旋轉增量角度, 加入 offsetAngle 參數
            inc = (2.*j*PI/n)*radian+offsetAngle;
            inc_ad = (2.*(j+1)*PI/n)*radian+offsetAngle;
            if (rd>rb)
            {
                // 當齒根半徑因為齒數增多後大於基圓半徑時, 漸開線從齒根圓長起
                theta = sqrt((rd*rd)/(rb*rb)-1.);
                inv = theta*radian-atan(theta);
                // 左側漸開線第1點座標
                // 左側輪廓線配合逆時針旋轉 inc 角度
                lfx = midx+rd*sin(inv-rot-inv_rp+inc);
                lfy = midy+rd*cos(inv-rot-inv_rp+inc);
                point1 = vector(lfx, lfy);
                lxd = lfx;
                lyd = lfy;
                lxd_ad = midx+rd*sin(inv-rot-inv_rp+inc_ad);
                lyd_ad = midy+rd*cos(inv-rot-inv_rp+inc_ad);
                // 右側漸開線第1點座標
                // 右側輪廓線配合順時針旋轉 inc 角度
                rfx = midx-rd*sin(inv-rot-inv_rp-inc);
                rfy = midy+rd*cos(inv-rot-inv_rp-inc);
                point2 = vector(rfx, rfy);
                rxd = rfx;
                ryd = rfy;
            }
            else
            {
                // 當基圓半徑大於齒根圓時, 漸開線從基圓長起
                //theta = sqrt((rb*rb)/(rb*rb)-1.);
                theta = 0;
                inv = theta*radian-atan(theta);
                // 左側漸開線第1點座標
                lfx = midx+rb*sin(inv-rot-inv_rp+inc);
                lfy = midy+rb*cos(inv-rot-inv_rp+inc);
                point1 = vector(lfx, lfy);
                lxd = midx+rd*sin(inv-rot-inv_rp+inc);
                lyd = midy+rd*cos(inv-rot-inv_rp+inc);
                lxd_ad = midx+rd*sin(inv-rot-inv_rp+inc_ad);
                lyd_ad = midy+rd*cos(inv-rot-inv_rp+inc_ad);
                // 左側從基圓點到齒根圓點, 畫直線 left from base point to dedendum point
                skLineSegment(gearSketch, "line_lbd" ~ nameId, {
                "start" : vector(lfx,lfy),
                "end" : vector((lxd),(lyd))
                });
                // 右側漸開線第1點座標
                rfx = midx-rb*sin(inv-rot-inv_rp-inc);
                rfy = midy+rb*cos(inv-rot-inv_rp-inc);
                point2 = vector(rfx, rfy);
                rxd = midx-rd*sin(inv-rot-inv_rp-inc);
                ryd = midy+rd*cos(inv-rot-inv_rp-inc);
                // 右側從基圓點到齒根圓點, 畫直線 right from base point to dedendum point
                skLineSegment(gearSketch, "line_rbd" ~ nameId, {
                "start" : vector(rfx,rfy),
                "end" : vector((rxd),(ryd))
                });
            }
            // 處理齒根的圓弧
            if (!arcDone) // create base arc between involutes once per tooth
            {
                var mid = getArcMidPoint(center2D, vector(lxd_ad,lyd_ad), vector(rxd,ryd)); // sketch arc is arc 3 points so need addtional point on arc

                if (mid != undefined) // if no base cylinder present (due to pressure angle), don't draw it
                {
                    // 齒根圓上的圓弧
                    skArc(gearSketch, "arc-d" ~ nameId, {
                                "start" : vector(lxd_ad,lyd_ad),
                                "mid" : mid,
                                "end" : vector(rxd,ryd)
                            });
                }
                if (rd>rb)
                {   
                    // 只有在齒根圓半徑大於基圓時, 將漸開線起點作為倒圓角的基準點
                    // find points in 3D space where edges need to be filleted later
                    filletEdges = append(filletEdges, toWorldVector(planeToCSys(sketchPlane), point2, definition.gearDepth, definition.flipGear));
                    filletEdges = append(filletEdges, toWorldVector(planeToCSys(sketchPlane), point1, definition.gearDepth, definition.flipGear));
                }
                else
                {
                    // 當小齒數時, 從基圓到齒根圓有一條直線, 因此倒圓角基準點必須以齒根圓上的點為基準
                    // find points in 3D space where edges need to be filleted later
                    filletEdges = append(filletEdges, toWorldVector(planeToCSys(sketchPlane), vector(rxd, ryd), definition.gearDepth, definition.flipGear));
                    filletEdges = append(filletEdges, toWorldVector(planeToCSys(sketchPlane), vector(lxd, lyd), definition.gearDepth, definition.flipGear));
                }
                // find area to extrude
                regionPoint = vector(point1[0] * 0.95 + center2D[0]*0.05, point1[1] * 0.95+center2D[1]*0.05, 0 * meter);
                arcDone = true;
            }
            // 將漸開線第1點存入 involute1 與 involute2 陣列中
            involute1 = append(involute1, point1);
            involute2 = append(involute2, point2);

            for (var i=1; i<imax+1; i+= 1)
            {
                // 先處理中線左側的漸開線
                // 當 rd 大於 rb 時, 漸開線並非畫至 rb, 而是 rd
                if (rd>rb)
                {
                    r = rd+i*dr;
                }
                else
                {
                    r = rb+i*dr;
                }
                theta = sqrt((r*r)/(rb*rb)-1);
                var inv = theta*radian-atan(theta);
                // 漸開線上的點座標
                xpt = midx+r*sin(inv-rot-inv_rp+inc);
                ypt = midy+r*cos(inv-rot-inv_rp+inc);
                point1 = vector(xpt, ypt);
                // 更新漸開線點座標
                lfx = xpt;
                lfy = ypt;
                //nameId += 1;
                involute1 = append(involute1, point1);
            }
            // 紀錄左側漸開線的最後一點, 也就是齒頂圓上的點座標
            var lastlx = xpt;
            var lastly = ypt;
            // another side
            for (var i=1; i<imax+1; i+= 1)
            {
                if (rd>rb)
                {
                    r = rd+i*dr;
                }
                else
                {
                    r = rb+i*dr;
                }
                theta = sqrt((r*r)/(rb*rb)-1);
                var inv = theta*radian-atan(theta);
                // 漸開線上的點座標
                xpt = midx-r*sin(inv-rot-inv_rp-inc);
                ypt = midy+r*cos(inv-rot-inv_rp-inc);
                point2 = vector(xpt, ypt);
                // 更新漸開線點座標
                rfx = xpt;
                rfy = ypt;
                //nameId += 1;
                involute2 = append(involute2, point2);
            }
            var lastrx = xpt;
            var lastry = ypt;

            // create involute sketch splines
            skFitSpline(gearSketch, "spline-a" ~ nameId, {
                        "points" : involute1
                    });
            skFitSpline(gearSketch, "spline-b" ~ nameId, {
                        "points" : involute2
                    });
            // 要注意, 若對調 vector(lastrx, lastry) 與 vector(lastlx, lastly) 則無法求得中點
            var mid_a = getArcMidPoint(center2D, vector(lastrx,lastry), vector(lastlx,lastly));
            if (mid_a != undefined)
            {
                skArc(gearSketch, "arc-a" ~ nameId, {
                            "start" : vector(lastlx,lastly),
                            "mid" : mid_a,
                            "end" : vector(lastrx,lastry)
                        }); 
            }
            nameId += 1;
        }
        if (definition.centerHole)
        {
            if (definition.key)
            {
                var keyVector = vector(0, 1);
                var perpKeyVector = vector(-1, 0);
                var keyHeight = (definition.keyHeight + definition.centerHoleDia) / 2;

                var points = [
                    center2D - (definition.keyWidth / 2) * perpKeyVector,
                    center2D - (definition.keyWidth / 2) * perpKeyVector + keyHeight * keyVector,
                    center2D + (definition.keyWidth / 2) * perpKeyVector + keyHeight * keyVector,
                    center2D + (definition.keyWidth / 2) * perpKeyVector];

                for (var i = 0; i < size(points); i += 1)
                {
                    skLineSegment(gearSketch, "line" ~ nameId,
                            { "start" : points[i],
                                "end" : points[(i + 1) % size(points)]
                            });
                    nameId += 1;
                }
            }

            // center hole circle sketch
            skCircle(gearSketch, "Center", {
                        "center" : center2D,
                        "radius" : definition.centerHoleDia / 2
                    });
        }
    skSolve(gearSketch);

    extrude(context, id + "extrude1", {
                "entities" : qContainsPoint(qCreatedBy(id + "gearSketch", EntityType.FACE), toWorld(planeToCSys(sketchPlane), regionPoint)),
                "endBound" : BoundingType.BLIND,
                "depth" : definition.gearDepth,
                "oppositeDirection" : definition.flipGear
            });


    var filletEdges3D = [];

    for (var i = 0; i < size(filletEdges); i += 1)
    {
        // Find the edges that intersect the points previously collected
        filletEdges3D = append(filletEdges3D, qContainsPoint(qCreatedBy(id + "extrude1", EntityType.EDGE), filletEdges[i]));
    }

    const filletRadius = norm(filletEdges[1] - filletEdges[0]) / 4; // arbitrary fillet size = one fourth the distance between the edges

    if (filletRadius >= 0.2 * millimeter) // arbitrary small size assuming tooling cannot hold a fillet radius smaller than this
    {

        try(opFillet(context, id + "fillet1", {
                        "entities" : qUnion(filletEdges3D),
                        "radius" : filletRadius
                    }));

    }

    // Remove sketch entities - no longer required
    opDeleteBodies(context, id + "delete", { "entities" : qCreatedBy(id + "gearSketch") });

    // created PCD sketch
    const PCDSketch = newSketchOnPlane(context, id + "PCDsketch", { "sketchPlane" : sketchPlane });
    skCircle(PCDSketch, "PCD", {
                "center" : center2D,
                "radius" : definition.pitchCircleDiameter / 2,
                "construction" : true
            });
    skSolve(PCDSketch);
    });

function getArcMidPoint(center is Vector, start is Vector, end is Vector)
{
    // need to convert 2D vectors back to 3D for vector angle calculation
    const center3D = vector(center[0], center[1], 0 * meter);
    const start3D = vector(start[0], start[1], 0 * meter);
    const end3D = vector(end[0], end[1], 0 * meter);

    const angle = vectorAngle(center3D - start3D, center3D - end3D) / 2;
    // if angle is less than zero then arc was flipped
    if (angle <= 0 * radian)
        return;
    start = center - start;

    var ca = cos(angle); // in radians
    var sa = sin(angle);
    return center - vector(ca * start[0] - sa * start[1], sa * start[0] + ca * start[1]);
}

function vectorAngle(vector1 is Vector, vector2 is Vector)
{
    // function assumes vectors are on a 2D plane so Z is always zero and the normal vector is always [0, 0, 1]
    return atan2(dot(vector(0, 0, 1), cross(vector1, vector2)), dot(vector1, vector2));
}

function toWorldVector(csys is CoordSystem, point is Vector, depth is map, direction is boolean) returns Vector
{
    var dir = direction ? -1 : 1;
    var vector3D = vector(point[0], point[1], dir * depth / 2);
    return toWorld(csys, vector3D);
}

export function editGearLogic(context is Context, id is Id, oldDefinition is map, definition is map, isCreating is boolean, specifiedParameters is map, hiddenBodies is Query) returns map
{
    // isCreating is required in the function definition for edit logic to work when editing an existing feature
    if (oldDefinition.numTeeth != definition.numTeeth)
    {
        definition.module = definition.pitchCircleDiameter / definition.numTeeth;
        definition.circularPitch = definition.module * PI;
        definition.diametralPitch = 1 * inch / definition.module;
        definition.teeth = toString(definition.numTeeth); //to name the feature
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
        definition.module = definition.pitchCircleDiameter / definition.numTeeth;
        definition.circularPitch = (PI * definition.pitchCircleDiameter) / definition.numTeeth;
        definition.diametralPitch = 1 * inch / definition.module;
        return definition;
    }

    if (oldDefinition.module != definition.module)
    {
        definition.circularPitch = definition.module * PI;
        definition.pitchCircleDiameter = definition.numTeeth * definition.module;
        definition.diametralPitch = 1 * inch / definition.module;
        return definition;
    }

    if (oldDefinition.diametralPitch != definition.diametralPitch)
    {
        definition.circularPitch = PI / (definition.diametralPitch / inch);
        definition.module = definition.circularPitch / PI;
        definition.pitchCircleDiameter = (definition.circularPitch * definition.numTeeth) / PI;
        return definition;
    }

    return definition;
}

const TEETH_BOUNDS =
{
            "min" : 4,
            "max" : 250,
            (unitless) : [4, 25, 250]
        } as IntegerBoundSpec;

const PRESSURE_ANGLE_BOUNDS =
{
            "min" : 12 * degree,
            "max" : 35 * degree,
            (degree) : [12, 20, 35]
        } as AngleBoundSpec;

const MODULE_BOUNDS =
{
            "min" : -TOLERANCE.zeroLength * meter,
            "max" : 500 * meter,
            (meter) : [1e-5, 0.001, 500],
            (centimeter) : 0.1,
            (millimeter) : 1.0,
            (inch) : 0.04
        } as LengthBoundSpec;

const CENTERHOLE_BOUNDS =
{
            "min" : -TOLERANCE.zeroLength * meter,
            "max" : 500 * meter,
            (meter) : [1e-5, 0.01, 500],
            (centimeter) : 1.0,
            (millimeter) : 10.0,
            (inch) : 0.375
        } as LengthBoundSpec;

const KEY_BOUNDS =
{
            "min" : -TOLERANCE.zeroLength * meter,
            "max" : 500 * meter,
            (meter) : [1e-5, 0.003, 500],
            (centimeter) : 0.3,
            (millimeter) : 3.0,
            (inch) : 0.125
        } as LengthBoundSpec;

export enum GearInputType
{
    annotation { "Name" : "Module" }
    module,
    annotation { "Name" : "Diametral pitch" }
    diametralPitch,
    annotation { "Name" : "Circular pitch" }
    circularPitch
}
