FeatureScript 3044;
import(path : "onshape/std/common.fs", version : "3044.0");

/**
 * Pulley Roller - 复合带轮滚轮
 *
 * 在一根空心管滚轮的端部圆环面上生成复合带轮零件（一个 part）：
 *   1. 按输入长度填充管子内孔（从所选端面向管内）
 *   2. 从端面沿轴向延伸主轴，默认直径与管子外径相同（与圆环面外环平齐）
 *   3. 沿轴排布多个 GT2 同步带轮，每个带轮可独立设置齿数 / 节圆直径 / 宽度，
 *      相邻带轮中心距可精确控制；每个带轮两侧有锥形挡边（法兰）防止带滑落，
 *      尺寸与 COTS 标准法兰带轮一致（按齿形标准固定，见 ToothProfileDefinitions
 *      的 FT/FH）：从齿顶锥形升起超过齿顶并保持，立在齿顶上方的锥形墙
 *   4. 端面底领：贴端面先是固定 1mm 厚的圆柱（盖住圆环面外环边），再以
 *      最厚 2mm 的锥体收拢到带轮 1 法兰直径；之后以该直径的圆柱引导段一直
 *      延伸到带轮 1 的法兰（平齐衔接）。底领 + 引导段 + 管内填充与其余
 *      新几何全部合并为一个 part
 *   5. 全部新几何合并为单一零件（独立 part，不与原管子合并）
 *
 * 齿形解析公式来自 trilobio 的 "Timing Belt Pulley"（GT2-2M / GT2-3M）。
 */
annotation { "Feature Type Name" : "Pulley Roller" }
export const pulleyRoller = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Roller end face",
                     "Filter" : GeometryType.PLANE && EntityType.FACE,
                     "MaxNumberOfPicks" : 1,
                     "Description" : "Annular end face of the tube. Pulley axis is normal to this face and passes through its center" }
        definition.rollerFace is Query;

        annotation { "Name" : "Flip direction",
                     "Description" : "Reverse pulley extension direction (default: outward from the selected end face)" }
        definition.flip is boolean;

        annotation { "Name" : "Fill tube length",
                     "Description" : "Solid fill length inside the tube from the end face (0 = no fill)" }
        isLength(definition.fillLength, FILL_BOUNDS);

        annotation { "Name" : "Number of pulleys" }
        isInteger(definition.pulleyCount, PULLEY_COUNT_BOUNDS);

        annotation { "Name" : "Tooth profile" }
        definition.toothProfile is ToothProfile;

        annotation { "Name" : "End collar overhang",
                     "Description" : "How far the collar extends beyond the tube OD / pulley-1 flange diameter at the end face (0 = flush). Collar: fixed 1mm cylinder + max-2mm taper; pulley flanges use COTS standard sizes" }
        isLength(definition.flangeOverhang, FLANGE_O_BOUNDS);

        annotation { "Name" : "Custom shaft diameter",
                     "Description" : "Check to set a custom shaft diameter. Default: shaft diameter = tube OD (flush with outer ring of the annular face)" }
        definition.customShaftDia is boolean;

        if (definition.customShaftDia)
        {
            annotation { "Name" : "Shaft diameter" }
            isLength(definition.shaftDiameter, SHAFT_DIA_BOUNDS);
        }

        annotation { "Name" : "Pulley 1 center offset",
                     "Description" : "Axial distance from end face to center of pulley 1" }
        isLength(definition.offset1, OFFSET_BOUNDS);

        annotation { "Name" : "Pulley 1 teeth" }
        isInteger(definition.teeth1, TEETH_BOUNDS);
        annotation { "Name" : "Pulley 1 pitch diameter",
                     "Description" : "Pitch diameter. Standard = teeth x pitch / PI; non-standard values scale the tooth profile" }
        isLength(definition.pd1, PD_BOUNDS);
        annotation { "Name" : "Pulley 1 width",
                     "Description" : "Pulley width (common standard: 6mm / 9mm)" }
        isLength(definition.width1, WIDTH_BOUNDS);

        annotation { "Name" : "Pulley 1-2 center distance",
                     "Description" : "Axial distance between pulley 1 and pulley 2 centers (used when Number of pulleys is 2 or more)" }
        isLength(definition.ctc1, CTC_BOUNDS);
        annotation { "Name" : "Pulley 2 teeth",
                     "Description" : "Used when Number of pulleys is 2 or more" }
        isInteger(definition.teeth2, TEETH_BOUNDS);
        annotation { "Name" : "Pulley 2 pitch diameter",
                     "Description" : "Used when Number of pulleys is 2 or more" }
        isLength(definition.pd2, PD_BOUNDS);
        annotation { "Name" : "Pulley 2 width",
                     "Description" : "Used when Number of pulleys is 2 or more" }
        isLength(definition.width2, WIDTH_BOUNDS);

        annotation { "Name" : "Pulley 2-3 center distance",
                     "Description" : "Axial distance between pulley 2 and pulley 3 centers (used when Number of pulleys is 3 or more)" }
        isLength(definition.ctc2, CTC_BOUNDS);
        annotation { "Name" : "Pulley 3 teeth",
                     "Description" : "Used when Number of pulleys is 3 or more" }
        isInteger(definition.teeth3, TEETH_BOUNDS);
        annotation { "Name" : "Pulley 3 pitch diameter",
                     "Description" : "Used when Number of pulleys is 3 or more" }
        isLength(definition.pd3, PD_BOUNDS);
        annotation { "Name" : "Pulley 3 width",
                     "Description" : "Used when Number of pulleys is 3 or more" }
        isLength(definition.width3, WIDTH_BOUNDS);

        annotation { "Name" : "Pulley 3-4 center distance",
                     "Description" : "Axial distance between pulley 3 and pulley 4 centers (used when Number of pulleys is 4)" }
        isLength(definition.ctc3, CTC_BOUNDS);
        annotation { "Name" : "Pulley 4 teeth",
                     "Description" : "Used when Number of pulleys is 4" }
        isInteger(definition.teeth4, TEETH_BOUNDS);
        annotation { "Name" : "Pulley 4 pitch diameter",
                     "Description" : "Used when Number of pulleys is 4" }
        isLength(definition.pd4, PD_BOUNDS);
        annotation { "Name" : "Pulley 4 width",
                     "Description" : "Used when Number of pulleys is 4" }
        isLength(definition.width4, WIDTH_BOUNDS);
    }
    {
        doPulleyRoller(context, id, definition);
    },
    {
        "rollerFace" : qNothing(),
        "flip" : false,
        "fillLength" : 20 * millimeter,
        "pulleyCount" : 2,
        "toothProfile" : ToothProfile.GT2_3M,
        "flangeOverhang" : 1 * millimeter,
        "customShaftDia" : false,
        "shaftDiameter" : 10 * millimeter,
        "offset1" : 12 * millimeter,
        "teeth1" : 28,
        "pd1" : 28 * 3 * millimeter / PI,
        "width1" : 6 * millimeter,
        "ctc1" : 20 * millimeter,
        "teeth2" : 32,
        "pd2" : 32 * 3 * millimeter / PI,
        "width2" : 6 * millimeter,
        "ctc2" : 20 * millimeter,
        "teeth3" : 24,
        "pd3" : 24 * 3 * millimeter / PI,
        "width3" : 6 * millimeter,
        "ctc3" : 20 * millimeter,
        "teeth4" : 24,
        "pd4" : 24 * 3 * millimeter / PI,
        "width4" : 6 * millimeter
    });

function doPulleyRoller(context is Context, id is Id, definition is map)
{
    if (isQueryEmpty(context, definition.rollerFace))
    {
        throw regenError("请选择管子端部的圆环面（Roller end face）。", ["rollerFace"]);
    }

    const face = evaluateQuery(context, definition.rollerFace)[0];

    // 轴向：所选平面面法线；圆心：由圆环边几何直接得出
    const facePlane = evPlane(context, { "face" : face });
    var axis = facePlane.normal;

    // 圆环面相邻边中过滤出真实的圆边（排除零长度接缝边），用圆定义得到半径与圆心
    const edges = evaluateQuery(context, qAdjacent(face, AdjacencyType.EDGE));
    var radii = [];
    var circleCenter = facePlane.origin;
    for (var i = 0; i < size(edges); i += 1)
    {
        if (evLength(context, { "entities" : edges[i] }) > 1e-6 * meter)
        {
            const def = evCurveDefinition(context, { "edge" : edges[i] });
            if (def.radius != undefined)
            {
                radii = append(radii, def.radius);
                circleCenter = def.coordSystem.origin;
            }
        }
    }
    if (size(radii) < 2)
    {
        throw regenError("所选面不是圆环面。请选择管子端部的环形端面（中间镂空）。", ["rollerFace"]);
    }
    const outerR = max(radii);
    const innerR = min(radii);
    const center = circleCenter;

    // 自动判断伸出方向：面在管子哪一端，带轮就往另一侧伸出（flip 可强制反转）
    const tubeBb = evBox3d(context, { "topology" : qOwnerBody(face) });
    const tubeMid = (tubeBb.minCorner + tubeBb.maxCorner) / 2;
    if (dot(center - tubeMid, axis) < 0)
    {
        axis = -axis;
    }
    if (definition.flip)
    {
        axis = -axis;
    }

    // 主轴半径
    var shaftR = outerR;
    if (definition.customShaftDia)
    {
        shaftR = definition.shaftDiameter / 2;
    }

    // 收集各带轮参数
    const profile = ToothProfileDefinitions[definition.toothProfile];
    var teeth = [definition.teeth1];
    var pds = [definition.pd1];
    var widths = [definition.width1];
    var centers = [definition.offset1];
    if (definition.pulleyCount >= 2)
    {
        teeth = append(teeth, definition.teeth2);
        pds = append(pds, definition.pd2);
        widths = append(widths, definition.width2);
        centers = append(centers, definition.ctc1);
    }
    if (definition.pulleyCount >= 3)
    {
        teeth = append(teeth, definition.teeth3);
        pds = append(pds, definition.pd3);
        widths = append(widths, definition.width3);
        centers = append(centers, definition.ctc2);
    }
    if (definition.pulleyCount >= 4)
    {
        teeth = append(teeth, definition.teeth4);
        pds = append(pds, definition.pd4);
        widths = append(widths, definition.width4);
        centers = append(centers, definition.ctc3);
    }

    const n = size(teeth);

    // 底领参数（仅作用于端面底领，径向超出量）；带轮法兰用 COTS 标准值（profile 的 FT/FH）
    const collarOverhang = definition.flangeOverhang;
    const FT = profile["FT"];
    const FH = profile["FH"];

    // 计算各带轮中心 z 位置（centers[0] 为面到带轮 1 中心，其后为相邻中心距增量）
    var z = [centers[0]];
    for (var i = 1; i < n; i += 1)
    {
        z = append(z, z[i - 1] + centers[i]);
    }

    // 校验：带轮（含 COTS 法兰）不得伸进管子、不得相互重叠、齿根必须粗于主轴
    if (z[0] < widths[0] / 2 + FT)
    {
        throw regenError("Pulley 1 center offset 太小（不小于带宽一半 + 法兰厚度，即 "
                ~ toString(widths[0] / 2 + FT) ~ "），否则左侧法兰会伸进管子。", ["offset1"]);
    }
    // 底领（圆柱 + 锥体收拢段）必须在带轮 1 端面之前完成，避免盖住齿形
    if (z[0] - widths[0] / 2 < COLLAR_CYL_LEN + COLLAR_CONE_LEN)
    {
        throw regenError("Pulley 1 center offset 太小：端面底领（圆柱 + 锥体）需在带轮 1 之前完成收拢，最小 offset ≈ "
                ~ toString(widths[0] / 2 + COLLAR_CYL_LEN + COLLAR_CONE_LEN) ~ "。", ["offset1"]);
    }
    for (var i = 1; i < n; i += 1)
    {
        if (z[i] - z[i - 1] < (widths[i] + widths[i - 1]) / 2 + 2 * FT)
        {
            throw regenError("带轮 " ~ toString(i) ~ " 与带轮 " ~ toString(i + 1)
                    ~ " 中心距太小（含法兰），两者重叠。最小值约 "
                    ~ toString((widths[i] + widths[i - 1]) / 2 + 2 * FT) ~ "。", ["ctc" ~ toString(i)]);
        }
    }
    for (var i = 0; i < n; i += 1)
    {
        const rootR = pulleyRootRadius(teeth[i], profile, pds[i]);
        if (shaftR >= rootR)
        {
            throw regenError("主轴直径对于带轮 " ~ toString(i + 1) ~ " 太大（齿根半径约 "
                    ~ toString(2 * rootR) ~ "）。请缩小轴径或增大带轮。");
        }
    }

    // 主轴长度 = 最后一个带轮末端 + 右侧 COTS 法兰
    const totalLen = z[n - 1] + widths[n - 1] / 2 + FT;

    var newBodies = [];

    // 1. 填充管子内孔
    if (definition.fillLength > 0 * millimeter)
    {
        const fillSketch = newSketchOnPlane(context, id + "fillSketch", {
                    "sketchPlane" : plane(center, axis)
                });
        skCircle(fillSketch, "fill", {
                    "center" : vector(0, 0) * millimeter,
                    "radius" : innerR
                });
        skSolve(fillSketch);
        opExtrude(context, id + "fillExtrude", {
                    "entities" : qSketchRegion(id + "fillSketch"),
                    "direction" : -axis,
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : definition.fillLength
                });
        newBodies = append(newBodies, qCreatedBy(id + "fillExtrude", EntityType.BODY));
    }

    // 2. 主轴：从端面延伸到最后一个带轮末端，默认与管外径平齐
    const shaftSketch = newSketchOnPlane(context, id + "shaftSketch", {
                "sketchPlane" : plane(center, axis)
            });
    skCircle(shaftSketch, "shaft", {
                "center" : vector(0, 0) * millimeter,
                "radius" : shaftR
            });
    skSolve(shaftSketch);
    opExtrude(context, id + "shaftExtrude", {
                "entities" : qSketchRegion(id + "shaftSketch"),
                "direction" : axis,
                "endBound" : BoundingType.BLIND,
                "endDepth" : totalLen
            });
    newBodies = append(newBodies, qCreatedBy(id + "shaftExtrude", EntityType.BODY));

    // 2b. 引导段：与带轮 1 法兰同直径（tipR + FH）的圆柱，从端面一直延伸到
    //     带轮 1 左法兰，与法兰平齐衔接
    const tipR0 = pulleyTipRadius(teeth[0], profile, pds[0]);
    const leadR = tipR0 + FH; // 引导段半径 = 带轮 1 法兰半径
    const leadLen = z[0] - widths[0] / 2; // 引导段长度：端面 -> 带轮 1 左端面
    const leadSketch = newSketchOnPlane(context, id + "leadSketch", {
                "sketchPlane" : plane(center, axis)
            });
    skCircle(leadSketch, "lead", {
                "center" : vector(0, 0) * millimeter,
                "radius" : leadR
            });
    skSolve(leadSketch);
    opExtrude(context, id + "leadExtrude", {
                "entities" : qSketchRegion(id + "leadSketch"),
                "direction" : axis,
                "endBound" : BoundingType.BLIND,
                "endDepth" : leadLen
            });
    newBodies = append(newBodies, qCreatedBy(id + "leadExtrude", EntityType.BODY));

    // 3. 各带轮：齿形草图（放在带轮起始端面）+ 单向拉伸一个宽度
    for (var i = 0; i < n; i += 1)
    {
        const pulleyPlane = plane(center + axis * (z[i] - widths[i] / 2), axis);
        drawPulleyTeeth(context, id + ("pulley" ~ toString(i)), pulleyPlane, teeth[i], profile, pds[i]);

        opExtrude(context, id + ("pulleyExtrude" ~ toString(i)), {
                    "entities" : qSketchRegion(id + ("pulley" ~ toString(i))),
                    "direction" : axis,
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : widths[i]
                });
        newBodies = append(newBodies, qCreatedBy(id + ("pulleyExtrude" ~ toString(i)), EntityType.BODY));
    }

    // 4. COTS 标准锥形法兰：每个带轮两侧各一圈，尺寸按齿形标准固定
    //    （FT 轴向厚度 / FH 高出齿顶的高度）：贴带轮端面处与齿顶平齐，
    //    锥形升起超过齿顶（tipR + FH）并保持到法兰外端 —— 立在齿顶上方的
    //    锥形墙，挡住带防止滑落（同 rollerstub / COTS 法兰带轮）
    for (var i = 0; i < n; i += 1)
    {
        const tipR = pulleyTipRadius(teeth[i], profile, pds[i]);
        const flangeR = tipR + FH; // 法兰墙半径（超过齿顶）
        const flangeIdL = id + ("flangeL" ~ toString(i));
        const flangeIdR = id + ("flangeR" ~ toString(i));

        // 左侧法兰：从带轮左端面（z[i] - widths[i]/2）沿 -axis 方向伸出 FT 厚
        makeFlange(context, flangeIdL, center, axis, z[i] - widths[i] / 2, -axis, FT, tipR, flangeR);
        newBodies = append(newBodies, qCreatedBy(flangeIdL + "revolve", EntityType.BODY));

        // 右侧法兰：从带轮右端面（z[i] + widths[i]/2）沿 axis 方向伸出 FT 厚
        makeFlange(context, flangeIdR, center, axis, z[i] + widths[i] / 2, axis, FT, tipR, flangeR);
        newBodies = append(newBodies, qCreatedBy(flangeIdR + "revolve", EntityType.BODY));
    }

    // 5. 底领：贴端面先是轴向厚度 1mm（COLLAR_CYL_LEN，固定）的圆柱（半径
    //    collarFace，盖住圆环面外环边），再以最厚 2mm（COLLAR_CONE_LEN，固定）
    //    的锥体收拢到带轮 1 法兰半径 leadR；之后引导段保持 leadR 直到带轮 1 法兰
    const collarFace = max(outerR, leadR) + collarOverhang; // 圆柱段半径
    const collarId = id + "collar";
    makeCollar(context, collarId, center, axis, COLLAR_CYL_LEN, COLLAR_CONE_LEN, collarFace, leadR);
    newBodies = append(newBodies, qCreatedBy(collarId + "revolve", EntityType.BODY));

    // 6. 合并：新几何（填充 + 主轴 + 各带轮 + 挡边 + 底领）合并为单一零件，不与原管子合并
    const allNew = evaluateQuery(context, qUnion(newBodies));
    if (size(allNew) > 1)
    {
        var rest = [allNew[1]];
        for (var k = 2; k < size(allNew); k += 1)
        {
            rest = append(rest, allNew[k]);
        }
        opBoolean(context, id + "union", {
                    "targets" : allNew[0],
                    "tools" : qUnion(rest),
                    "operationType" : BooleanOperationType.UNION
                });
    }
}

/**
 * 在给定平面上画出完整带轮齿形草图（t 个齿沿圆周闭合）。
 * 齿形点由 GT 标准参数解析求出，再按 scale = pd / (t*P/PI) 等比缩放到用户节圆直径。
 * 返回齿根半径（缩放后）。
 */
function drawPulleyTeeth(context is Context, id is Id, sketchPlane is Plane, t is number, profile is map, pd is ValueWithUnits)
{
    const P = profile["P"];
    const scale = pd / (t * P / PI);
    const pts = computeGtToothPoints(t, profile);
    const alpha = pts.alpha;

    const sk = newSketchOnPlane(context, id, { "sketchPlane" : sketchPlane });

    for (var i = 0; i < t; i += 1)
    {
        const iString = toString(i);
        const rMat = [[cos(i * alpha), -sin(i * alpha)], [sin(i * alpha), cos(i * alpha)]] as Matrix;

        skArc(sk, "arcAB" ~ iString, {
                    "start" : rMat * (pts.A * scale),
                    "mid" : rMat * (pts.ABM * scale),
                    "end" : rMat * (pts.B * scale)
                });
        skArc(sk, "arcBC" ~ iString, {
                    "start" : rMat * (pts.B * scale),
                    "mid" : rMat * (pts.BCM * scale),
                    "end" : rMat * (pts.C * scale)
                });
        skArc(sk, "arcCD" ~ iString, {
                    "start" : rMat * (pts.C * scale),
                    "mid" : rMat * (pts.CDM * scale),
                    "end" : rMat * (pts.D * scale)
                });
        skArc(sk, "arcDE" ~ iString, {
                    "start" : rMat * (pts.D * scale),
                    "mid" : rMat * (pts.DEM * scale),
                    "end" : rMat * (pts.E * scale)
                });
        skArc(sk, "arcEF" ~ iString, {
                    "start" : rMat * (pts.E * scale),
                    "mid" : rMat * (pts.EFM * scale),
                    "end" : rMat * (pts.F * scale)
                });
        skArc(sk, "arcFG" ~ iString, {
                    "start" : rMat * (pts.F * scale),
                    "mid" : rMat * (pts.FGM * scale),
                    "end" : rMat * (pts.G * scale)
                });
    }
    skSolve(sk);

    return norm(pts.ABM) * scale;
}

/**
 * 带轮齿根半径（缩放前由齿数与标准 pitch 决定，再按 pd 缩放）。
 */
function pulleyRootRadius(t is number, profile is map, pd is ValueWithUnits) returns ValueWithUnits
{
    const pts = computeGtToothPoints(t, profile);
    const scale = pd / (t * profile["P"] / PI);
    return norm(pts.ABM) * scale;
}

/**
 * 带轮齿顶（外圆）半径（按 pd 缩放后）。D 点位于以轴心为圆心的齿顶圆上。
 */
function pulleyTipRadius(t is number, profile is map, pd is ValueWithUnits) returns ValueWithUnits
{
    const pts = computeGtToothPoints(t, profile);
    const scale = pd / (t * profile["P"] / PI);
    return norm(pts.D) * scale;
}

/**
 * 带轮锥形法兰（旋转成型）：
 *   起始于 zFace（轴向位置，贴带轮端面），沿 xDir 方向伸出 ft 厚。
 *   贴带轮端面处与齿顶平齐（rFace），先以约 45 度锥面快速升起超过齿顶
 *   （升高 rOuter - rFace），之后保持 rOuter 直到法兰外端 —— 立在齿顶
 *   上方的锥形墙，挡住带防止滑落。
 * 截面草图放在过 center + axis*zFace、x 轴为 xDir 的平面上，绕轴线整周旋转。
 */
function makeFlange(context is Context, id is Id, center is Vector, axis is Vector, zFace is ValueWithUnits, xDir is Vector, ft is ValueWithUnits, rFace is ValueWithUnits, rOuter is ValueWithUnits)
{
    const base = center + axis * zFace;
    const sk = newSketchOnPlane(context, id + "sketch", {
                "sketchPlane" : plane(base, perpendicularVector(axis), xDir)
            });

    // 截面轮廓（x = 沿 xDir 的轴向距离，y = 半径），y=0 边位于旋转轴上
    var points;
    if (rOuter > rFace && rOuter - rFace < ft)
    {
        // 法兰：45 度锥面升起段（轴向 = 径向升高量）+ 保持段（锥形墙立住超过齿顶）
        const rise = rOuter - rFace;
        points = [
                    vector(0 * millimeter, 0 * millimeter),
                    vector(0 * millimeter, rFace),
                    vector(rise, rOuter),
                    vector(ft, rOuter),
                    vector(ft, 0 * millimeter)
                ];
    }
    else
    {
        // 纯锥形：升高量超过厚度时法兰为全锥（正常 COTS 值不会走到该分支）
        points = [
                    vector(0 * millimeter, 0 * millimeter),
                    vector(0 * millimeter, rFace),
                    vector(ft, rOuter),
                    vector(ft, 0 * millimeter)
                ];
    }

    for (var j = 0; j < size(points); j += 1)
    {
        skLineSegment(sk, "l" ~ toString(j), {
                    "start" : points[j],
                    "end" : points[(j + 1) % size(points)]
                });
    }
    skSolve(sk);

    opRevolve(context, id + "revolve", {
                "entities" : qCreatedBy(id + "sketch", EntityType.FACE),
                "axis" : line(center, axis),
                "angleForward" : 360 * degree
            });
}

/**
 * 端面底领（旋转成型）：贴所选端面先是轴向厚度 cylLen 的圆柱（半径 rFace，
 * 盖住圆环面外环边），随后锥体在轴向 coneLen（最厚 2mm）内收拢到 rOuter
 * （带轮 1 法兰半径），与引导段圆柱平齐衔接；之后引导段保持 rOuter 直到
 * 带轮 1 法兰。截面草图平面过 center 且包含轴线（x 轴沿 axis 方向），
 * 绕轴线整周旋转。
 */
function makeCollar(context is Context, id is Id, center is Vector, axis is Vector, cylLen is ValueWithUnits, coneLen is ValueWithUnits, rFace is ValueWithUnits, rOuter is ValueWithUnits)
{
    const sk = newSketchOnPlane(context, id + "sketch", {
                "sketchPlane" : plane(center, perpendicularVector(axis), axis)
            });

    // 截面轮廓（x = 沿 axis 的轴向距离，y = 半径），y=0 边位于旋转轴上
    const points = [
                vector(0 * millimeter, 0 * millimeter),
                vector(0 * millimeter, rFace),
                vector(cylLen, rFace),
                vector(cylLen + coneLen, rOuter),
                vector(cylLen + coneLen, 0 * millimeter)
            ];

    for (var j = 0; j < size(points); j += 1)
    {
        skLineSegment(sk, "l" ~ toString(j), {
                    "start" : points[j],
                    "end" : points[(j + 1) % size(points)]
                });
    }
    skSolve(sk);

    opRevolve(context, id + "revolve", {
                "entities" : qCreatedBy(id + "sketch", EntityType.FACE),
                "axis" : line(center, axis),
                "angleForward" : 360 * degree
            });
}

/**
 * GT2 齿形单齿特征点解析解（来自 trilobio "Timing Belt Pulley"）。
 * 坐标以带轮轴心为原点；A..D 为齿槽一侧，DE 为以原点为圆心的齿顶外圆弧，
 * E..G 为旋转 -2π/t 后的另一侧，t 个齿首尾相接铺满整圈。
 */
function computeGtToothPoints(t is number, p is map)
{
    const P = p["P"];
    const RAB = p["R3"];
    const RBC = p["R2"];
    const RCD = p["R1"];
    const b = p["b"];
    const h = p["h"];
    const PLD = p["PLD"];

    const alpha = 2 * P / (P * t / PI) * radian;

    const AX = (-2 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 + RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) + RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) - 2 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 + RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) + RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) + 256 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 18 - 1152 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 16 + 2112 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 + 256 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 * cos(radian * PI * b / (P * t)) ^ 6 - 2016 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 - 384 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 * cos(radian * PI * b / (P * t)) ^ 6 + 1056 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 + 192 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 * cos(radian * PI * b / (P * t)) ^ 6 - 288 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 - 32 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 6 * cos(radian * PI * b / (P * t)) ^ 6 + 32 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 6) * sin(radian * 2 * PI * b / (P * t)) / (RAB ^ 2 - RBC ^ 2);
    const AY = (4 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 4 - 4 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 + RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) - 2 * RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) * sin(radian * PI * b / (P * t)) ^ 2 + RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) + 4 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 4 - 4 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 - 2 * RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) * sin(radian * PI * b / (P * t)) ^ 2 + RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) - RBC ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) - 512 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 20 + 2560 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 18 - 5248 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 16 - 512 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 * cos(radian * PI * b / (P * t)) ^ 6 + 5632 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 + 1024 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 * cos(radian * PI * b / (P * t)) ^ 6 - 3328 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 - 640 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 * cos(radian * PI * b / (P * t)) ^ 6 + 1024 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 + 128 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 * cos(radian * PI * b / (P * t)) ^ 6 - 128 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8) / (RAB ^ 2 - RBC ^ 2);
    const BX = (2 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 - RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) - RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) + 2 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 - RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) - RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) - 256 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 18 + 1152 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 16 - 2112 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 - 256 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 * cos(radian * PI * b / (P * t)) ^ 6 + 2016 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 + 384 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 * cos(radian * PI * b / (P * t)) ^ 6 - 1056 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 - 192 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 * cos(radian * PI * b / (P * t)) ^ 6 + 288 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 + 32 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 6 * cos(radian * PI * b / (P * t)) ^ 6 - 32 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 6) * sin(radian * 2 * PI * b / (P * t)) / (RAB ^ 2 - RBC ^ 2);
    const BY = (4 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 4 - 4 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 + RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) - 2 * RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) * sin(radian * PI * b / (P * t)) ^ 2 + RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) + 4 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 4 - 4 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 - 2 * RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) * sin(radian * PI * b / (P * t)) ^ 2 + RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) - RBC ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) - 512 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 20 + 2560 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 18 - 5248 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 16 - 512 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 * cos(radian * PI * b / (P * t)) ^ 6 + 5632 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 + 1024 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 * cos(radian * PI * b / (P * t)) ^ 6 - 3328 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 - 640 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 * cos(radian * PI * b / (P * t)) ^ 6 + 1024 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 + 128 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 * cos(radian * PI * b / (P * t)) ^ 6 - 128 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8) / (RAB ^ 2 - RBC ^ 2);
    const ABX = 0 * meter;
    const ABY = P * t / (2 * PI) - PLD + RAB - h;
    const BCX = (-P * t / (2 * PI) + PLD - RAB + h + 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 - sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2)) * sin(radian * 2 * PI * b / (P * t));
    const BCY = (P * t / (2 * PI) - PLD + RAB - h - 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 + sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2)) * cos(radian * 2 * PI * b / (P * t));
    const CDX = (4 * BCX ^ 2 + 4 * BCY ^ 2 - 8 * BCY * (-BCX * sqrt(-(4 * BCX ^ 2 + 4 * BCY ^ 2 - P ^ 2 * t ^ 2 / PI ^ 2 + 4 * P * PLD * t / PI - 4 * P * RBC * t / PI - 4 * PLD ^ 2 + 8 * PLD * RBC - 4 * RBC ^ 2) * (4 * BCX ^ 2 + 4 * BCY ^ 2 - P ^ 2 * t ^ 2 / PI ^ 2 + 4 * P * PLD * t / PI + 4 * P * RBC * t / PI + 8 * P * RCD * t / PI - 4 * PLD ^ 2 - 8 * PLD * RBC - 16 * PLD * RCD - 4 * RBC ^ 2 - 16 * RBC * RCD - 16 * RCD ^ 2)) / (8 * (BCX ^ 2 + BCY ^ 2)) + BCY * (4 * BCX ^ 2 + 4 * BCY ^ 2 + P ^ 2 * t ^ 2 / PI ^ 2 - 4 * P * PLD * t / PI - 4 * P * RCD * t / PI + 4 * PLD ^ 2 + 8 * PLD * RCD - 4 * RBC ^ 2 - 8 * RBC * RCD) / (8 * (BCX ^ 2 + BCY ^ 2))) + P ^ 2 * t ^ 2 / PI ^ 2 - 4 * P * PLD * t / PI - 4 * P * RCD * t / PI + 4 * PLD ^ 2 + 8 * PLD * RCD - 4 * RBC ^ 2 - 8 * RBC * RCD) / (8 * BCX);
    const CDY = -BCX * sqrt(-(4 * BCX ^ 2 + 4 * BCY ^ 2 - P ^ 2 * t ^ 2 / PI ^ 2 + 4 * P * PLD * t / PI - 4 * P * RBC * t / PI - 4 * PLD ^ 2 + 8 * PLD * RBC - 4 * RBC ^ 2) * (4 * BCX ^ 2 + 4 * BCY ^ 2 - P ^ 2 * t ^ 2 / PI ^ 2 + 4 * P * PLD * t / PI + 4 * P * RBC * t / PI + 8 * P * RCD * t / PI - 4 * PLD ^ 2 - 8 * PLD * RBC - 16 * PLD * RCD - 4 * RBC ^ 2 - 16 * RBC * RCD - 16 * RCD ^ 2)) / (8 * (BCX ^ 2 + BCY ^ 2)) + BCY * (4 * BCX ^ 2 + 4 * BCY ^ 2 + P ^ 2 * t ^ 2 / PI ^ 2 - 4 * P * PLD * t / PI - 4 * P * RCD * t / PI + 4 * PLD ^ 2 + 8 * PLD * RCD - 4 * RBC ^ 2 - 8 * RBC * RCD) / (8 * (BCX ^ 2 + BCY ^ 2));
    const CX = CDX - RCD * (-BCX + CDX) / (RBC + RCD);
    const CY = CDY - RCD * (-BCY + CDY) / (RBC + RCD);
    const CDR = sqrt(CDX ^ 2 + CDY ^ 2);
    const DX = CDX * (CDR + RCD) / CDR;
    const DY = CDY * (CDR + RCD) / CDR;
    const TANG = -PI / t;
    const EX = -sqrt(DX ^ 2 + DY ^ 2) * sin(radian * 2 * TANG + asin(DX / sqrt(DX ^ 2 + DY ^ 2)));
    const EY = sqrt(DX ^ 2 + DY ^ 2) * cos(radian * 2 * TANG + asin(DX / sqrt(DX ^ 2 + DY ^ 2)));
    const FX = -sqrt(CX ^ 2 + CY ^ 2) * sin(radian * 2 * TANG + asin(CX / sqrt(CX ^ 2 + CY ^ 2)));
    const FY = sqrt(CX ^ 2 + CY ^ 2) * cos(radian * 2 * TANG + asin(CX / sqrt(CX ^ 2 + CY ^ 2)));
    const EFX = -sqrt(CDX ^ 2 + CDY ^ 2) * sin(radian * 2 * TANG + asin(CDX / sqrt(CDX ^ 2 + CDY ^ 2)));
    const EFY = sqrt(CDX ^ 2 + CDY ^ 2) * cos(radian * 2 * TANG + asin(CDX / sqrt(CDX ^ 2 + CDY ^ 2)));
    const GX = -sqrt(BX ^ 2 + BY ^ 2) * sin(radian * 2 * TANG + asin(BX / sqrt(BX ^ 2 + BY ^ 2)));
    const GY = sqrt(BX ^ 2 + BY ^ 2) * cos(radian * 2 * TANG + asin(BX / sqrt(BX ^ 2 + BY ^ 2)));
    const FGX = -sqrt(BCX ^ 2 + BCY ^ 2) * sin(radian * 2 * TANG + asin(BCX / sqrt(BCX ^ 2 + BCY ^ 2)));
    const FGY = sqrt(BCX ^ 2 + BCY ^ 2) * cos(radian * 2 * TANG + asin(BCX / sqrt(BCX ^ 2 + BCY ^ 2)));

    const A = vector(AX, AY);
    const B = vector(BX, BY);
    const AB = vector(ABX, ABY);
    const C = vector(CX, CY);
    const BC = vector(BCX, BCY);
    const D = vector(DX, DY);
    const CD = vector(CDX, CDY);
    const E = vector(EX, EY);
    const F = vector(FX, FY);
    const EF = vector(EFX, EFY);
    const FG = vector(FGX, FGY);
    const G = vector(GX, GY);

    // 弧中点（Onshape skArc 以起点/中点/终点定义圆弧）
    const ABM = getArcMidPointShorter(A, B, AB);
    const BCM = getArcMidPointShorter(B, C, BC);
    const CDM = getArcMidPointShorter(C, D, CD);
    const DEM = getArcMidPointShorter(D, E, vector(0, 0) * meter);
    const EFM = getArcMidPointShorter(E, F, EF);
    const FGM = getArcMidPointShorter(F, G, FG);

    return {
                "A" : A,
                "B" : B,
                "C" : C,
                "D" : D,
                "E" : E,
                "F" : F,
                "G" : G,
                "ABM" : ABM,
                "BCM" : BCM,
                "CDM" : CDM,
                "DEM" : DEM,
                "EFM" : EFM,
                "FGM" : FGM,
                "alpha" : alpha
            };
}

function getArcMidPointShorter(start is Vector, end is Vector, center is Vector)
{
    const radius = norm(start - center);
    const mid_vec = ((start - center) + (end - center)) / 2;
    const mv_radius = norm(mid_vec);
    return center + radius * mid_vec / mv_radius;
}

const FILL_BOUNDS =
{
            (meter) : [0, 0.02, 10],
            (centimeter) : 2,
            (millimeter) : 20,
            (inch) : 0.75
        } as LengthBoundSpec;

const PULLEY_COUNT_BOUNDS =
{
            (unitless) : [1, 2, 4]
        } as IntegerBoundSpec;

const SHAFT_DIA_BOUNDS =
{
            (millimeter) : [2, 10, 500],
            (inch) : 0.375
        } as LengthBoundSpec;

const OFFSET_BOUNDS =
{
            (millimeter) : [1, 12, 500],
            (inch) : 0.5
        } as LengthBoundSpec;

const TEETH_BOUNDS =
{
            (unitless) : [10, 28, 200]
        } as IntegerBoundSpec;

const PD_BOUNDS =
{
            (millimeter) : [5, 26.74, 500],
            (inch) : 1.0
        } as LengthBoundSpec;

const WIDTH_BOUNDS =
{
            (millimeter) : [2, 6, 100],
            (inch) : 0.25
        } as LengthBoundSpec;

const CTC_BOUNDS =
{
            (millimeter) : [5, 20, 500],
            (inch) : 0.75
        } as LengthBoundSpec;

// 端面底领尺寸（固定）：圆柱段轴向厚度 1mm，锥体收拢段最厚 2mm
const COLLAR_CYL_LEN = 1 * millimeter;
const COLLAR_CONE_LEN = 2 * millimeter;

const FLANGE_O_BOUNDS =
{
            (millimeter) : [0, 1, 20],
            (inch) : 0.04
        } as LengthBoundSpec;

export enum ToothProfile
{
    annotation { "Name" : "GT2-3M (3mm pitch)" }
    GT2_3M,
    annotation { "Name" : "GT2-2M (2mm pitch)" }
    GT2_2M
}

// FT = 带轮法兰厚度（轴向），FH = 法兰墙高出齿顶的高度 —— 与 COTS 标准
// 法兰 GT2 带轮一致（如 SDP / Gates 法兰带轮：法兰锥形升起超过齿顶并保持）
const ToothProfileDefinitions = {
        (ToothProfile.GT2_2M) : {
            "P" : 2 * millimeter,
            "R1" : 0.15 * millimeter,
            "R2" : 1 * millimeter,
            "R3" : 0.555 * millimeter,
            "b" : 0.4 * millimeter,
            "H" : 1.38 * millimeter,
            "h" : 0.75 * millimeter,
            "i" : 0.63 * millimeter,
            "PLD" : 0.254 * millimeter,
            "FT" : 1 * millimeter,
            "FH" : 0.8 * millimeter
        },
        (ToothProfile.GT2_3M) : {
            "P" : 3 * millimeter,
            "R1" : 0.25 * millimeter,
            "R2" : 1.52 * millimeter,
            "R3" : 0.85 * millimeter,
            "b" : 0.61 * millimeter,
            "H" : 2.4 * millimeter,
            "h" : 1.14 * millimeter,
            "i" : 1.26 * millimeter,
            "PLD" : 0.381 * millimeter,
            "FT" : 1.5 * millimeter,
            "FH" : 1.2 * millimeter
        },
    };
