// ============================================================================
// StudyGraph — script tạo lược đồ ArangoDB
//
// Cách chạy:
//   1) Tạo database (chạy khi đang ở _system):
//        arangosh --server.database _system ^
//                 --javascript.execute-string "db._createDatabase('studygraph');"
//   2) Chạy script này trên database studygraph:
//        arangosh --server.database studygraph ^
//                 --javascript.execute schema/01_create_schema.js
//
// Script LŨY ĐẲNG: chạy lại nhiều lần không lỗi, không xoá dữ liệu.
//
// QUAN TRỌNG: trước đây repo KHÔNG có script này, nên 4 chỉ mục dưới đây chưa
// từng được tạo. Hệ quả cụ thể: unique index [_from,_to] trên enrolled_in
// không tồn tại, nên khối catch bắt lỗi 1210 trong CoursesController.Enroll
// KHÔNG BAO GIỜ chạy — ghi danh trùng sẽ tạo hai cạnh thay vì trả 409 Conflict.
// ============================================================================

const graphModule = require("@arangodb/general-graph");

const DOC_COLLECTIONS  = ["users", "courses", "lessons", "quizzes"];
const EDGE_COLLECTIONS = ["enrolled_in", "completed", "prerequisite_of", "rated"];
const GRAPH_NAME       = "studygraph_graph";

function log(msg) { print("  " + msg); }

// ---------------------------------------------------------------------------
// 1. Document collections (đỉnh)
// ---------------------------------------------------------------------------
print("[1/4] Document collections");
for (const name of DOC_COLLECTIONS) {
  if (db._collection(name) === null) { db._create(name); log("tạo " + name); }
  else                               { log("đã có " + name); }
}

// ---------------------------------------------------------------------------
// 2. Edge collections (cạnh)
//    Edge index trên _from và _to được ArangoDB tạo TỰ ĐỘNG cho mọi edge
//    collection — đây là chỉ mục quan trọng nhất của hệ thống, vì nó làm cho
//    chi phí traversal độc lập với tổng kích thước dữ liệu.
// ---------------------------------------------------------------------------
print("[2/4] Edge collections");
for (const name of EDGE_COLLECTIONS) {
  if (db._collection(name) === null) { db._createEdgeCollection(name); log("tạo " + name); }
  else                               { log("đã có " + name); }
}

// ---------------------------------------------------------------------------
// 3. Chỉ mục khai báo thêm
// ---------------------------------------------------------------------------
print("[3/4] Chỉ mục");

// Ràng buộc nghiệp vụ: email không trùng.
// UserRepository.GetByEmailAsync (đăng nhập) dựa vào tính duy nhất này.
db.users.ensureIndex({
  type: "persistent", fields: ["Email"], unique: true,
  name: "idx_users_email"
});
log("idx_users_email (unique)");

// Phục vụ lọc danh sách khóa theo chủ đề + trình độ (CourseRepository.ListAql)
db.courses.ensureIndex({
  type: "persistent", fields: ["Category", "Level"],
  name: "idx_courses_category_level"
});
log("idx_courses_category_level");

// Phục vụ lấy bài học theo khóa đã sắp thứ tự, và phép tra "bài trước"
// (EnrollmentRepository.PreviousLessonCompletedAql lọc theo CourseKey + Order).
db.lessons.ensureIndex({
  type: "persistent", fields: ["CourseKey", "Order"],
  name: "idx_lessons_course_order"
});
log("idx_lessons_course_order");

// Phục vụ QuizRepository.GetByLessonAql — LessonsController và QuizzesController
// đều tra quiz theo LessonKey ở mỗi request.
db.quizzes.ensureIndex({
  type: "persistent", fields: ["LessonKey"],
  name: "idx_quizzes_lesson"
});
log("idx_quizzes_lesson");

// Chống ghi danh trùng Ở MỨC CƠ SỞ DỮ LIỆU.
// Vi phạm sinh lỗi ArangoDB 1210 (ERROR_ARANGO_UNIQUE_CONSTRAINT_VIOLATED),
// được CoursesController.Enroll bắt và chuyển thành HTTP 409 Conflict.
// Lý do không kiểm tra ở tầng ứng dụng: hai request đồng thời đều đọc thấy
// "chưa tồn tại" rồi cả hai đều chèn (race condition).
db.enrolled_in.ensureIndex({
  type: "persistent", fields: ["_from", "_to"], unique: true,
  name: "idx_enrolled_unique"
});
log("idx_enrolled_unique (unique) — chống ghi danh trùng");

// CHÚ Ý: KHÔNG đặt unique index trên `completed`.
// Nghiệp vụ "làm lại quiz để cải thiện điểm" là hợp lệ, nên ở đó
// EnrollmentRepository dùng UPSERT để phép ghi có tính lũy đẳng.

// ---------------------------------------------------------------------------
// 4. Named graph
//    Khai báo tường minh tập cạnh nào nối tập đỉnh nào → ArangoDB kiểm tra
//    được toàn vẹn tham chiếu của cạnh, và Web UI vẽ được graph viewer.
// ---------------------------------------------------------------------------
print("[4/4] Named graph " + GRAPH_NAME);
const relations = [
  graphModule._relation("enrolled_in",     ["users"],   ["courses"]),
  graphModule._relation("completed",       ["users"],   ["lessons"]),
  graphModule._relation("rated",           ["users"],   ["courses"]),
  graphModule._relation("prerequisite_of", ["courses"], ["courses"])
];

if (graphModule._list().indexOf(GRAPH_NAME) !== -1) {
  log("đã có — bỏ qua (xoá bằng: graphModule._drop('" + GRAPH_NAME + "', false))");
} else {
  graphModule._create(GRAPH_NAME, relations);
  log("tạo xong với 4 edge definition");
}

// ---------------------------------------------------------------------------
// Kiểm tra
// ---------------------------------------------------------------------------
print("");
print("ArangoDB version : " + db._query("RETURN VERSION()").toArray()[0]);
print("Database         : " + db._name());
print("Collections      : " + db._collections()
        .map(c => c.name()).filter(n => n[0] !== "_").sort().join(", "));
print("Graph edge cols  : " + graphModule._graph(GRAPH_NAME)
        ._edgeCollections().map(c => c.name()).sort().join(", "));
print("");
print("Bước tiếp theo: cd StudyGraph.Seeder && dotnet run -- --json");
