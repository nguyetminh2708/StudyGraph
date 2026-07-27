-- ===========================================================================
-- StudyGraph — lược đồ quan hệ đối chứng trên SQL Server (Phụ lục B)
--
-- Chạy: sqlcmd -S "(localdb)\MSSQLLocalDB" -i sql\01_schema.sql
--   hoặc mở trong SSMS / Azure Data Studio và Execute.
--
-- Lược đồ đạt 3NF (Mục 3.4). Hai bảng CourseTags và QuizQuestions sinh ra từ
-- bước chuẩn hoá 1NF: cấu trúc lồng mà document store của ArangoDB lưu trong
-- MỘT tài liệu thì mô hình quan hệ phải rải ra HAI quan hệ.
--
-- Nguyên tắc N3 (Mục 6.1): mọi trường mà ArangoDB được lợi từ chỉ mục thì bên
-- này cũng có chỉ mục tương đương — kể cả chỉ mục phụ theo chiều nghịch trên
-- bảng kết, vì edge index của ArangoDB đánh chỉ mục CẢ _from và _to.
-- ===========================================================================

IF DB_ID('StudyGraphSql') IS NULL
    CREATE DATABASE StudyGraphSql;
GO
USE StudyGraphSql;
GO

-- Xoá theo thứ tự ngược phụ thuộc khoá ngoại (script lũy đẳng)
IF OBJECT_ID('dbo.Ratings',       'U') IS NOT NULL DROP TABLE dbo.Ratings;
IF OBJECT_ID('dbo.Prerequisites', 'U') IS NOT NULL DROP TABLE dbo.Prerequisites;
IF OBJECT_ID('dbo.Completions',   'U') IS NOT NULL DROP TABLE dbo.Completions;
IF OBJECT_ID('dbo.Enrollments',   'U') IS NOT NULL DROP TABLE dbo.Enrollments;
IF OBJECT_ID('dbo.QuizQuestions', 'U') IS NOT NULL DROP TABLE dbo.QuizQuestions;
IF OBJECT_ID('dbo.Quizzes',       'U') IS NOT NULL DROP TABLE dbo.Quizzes;
IF OBJECT_ID('dbo.Lessons',       'U') IS NOT NULL DROP TABLE dbo.Lessons;
IF OBJECT_ID('dbo.CourseTags',    'U') IS NOT NULL DROP TABLE dbo.CourseTags;
IF OBJECT_ID('dbo.Courses',       'U') IS NOT NULL DROP TABLE dbo.Courses;
IF OBJECT_ID('dbo.Users',         'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-- ===========================================================================
-- 1. Bảng thực thể  (~ 4 document collection)
-- ===========================================================================
CREATE TABLE Users (                        -- ~ collection users
    UserKey   VARCHAR(20)   NOT NULL,
    Name      NVARCHAR(100) NOT NULL,
    Email     VARCHAR(200)  NOT NULL,
    Role      VARCHAR(20)   NOT NULL
        CONSTRAINT CK_Users_Role CHECK (Role IN ('student','admin')),
    CreatedAt DATETIME2     NOT NULL,
    CONSTRAINT PK_Users      PRIMARY KEY (UserKey),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)   -- ~ idx_users_email
);
GO

CREATE TABLE Courses (                      -- ~ collection courses
    CourseKey   VARCHAR(40)    NOT NULL,
    Title       NVARCHAR(200)  NOT NULL,
    Category    VARCHAR(40)    NOT NULL,
    Level       INT            NOT NULL
        CONSTRAINT CK_Courses_Level CHECK (Level BETWEEN 1 AND 3),
    Description NVARCHAR(1000) NULL,
    CONSTRAINT PK_Courses PRIMARY KEY (CourseKey)
);
CREATE INDEX IX_Courses_Category_Level       -- ~ idx_courses_category_level
    ON Courses (Category, Level);
GO

-- Tách từ mảng Course.Tags — bước chuẩn hoá 1NF
CREATE TABLE CourseTags (
    CourseKey VARCHAR(40) NOT NULL,
    Tag       VARCHAR(40) NOT NULL,
    CONSTRAINT PK_CourseTags PRIMARY KEY (CourseKey, Tag),
    CONSTRAINT FK_CourseTags_Courses FOREIGN KEY (CourseKey)
        REFERENCES Courses(CourseKey) ON DELETE CASCADE
);
GO

CREATE TABLE Lessons (                      -- ~ collection lessons
    LessonKey VARCHAR(60)   NOT NULL,
    CourseKey VARCHAR(40)   NOT NULL,
    Title     NVARCHAR(200) NOT NULL,
    Ord       INT           NOT NULL,       -- ~ Lesson.Order (Order là từ khoá SQL)
    Content   NVARCHAR(MAX) NULL,
    CONSTRAINT PK_Lessons PRIMARY KEY (LessonKey),
    -- Khoá ngoại này KHÔNG có đối ứng bên ArangoDB: ở đó CourseKey là thuộc
    -- tính chứ không phải cạnh nên không được kiểm tra (hạn chế H-17).
    CONSTRAINT FK_Lessons_Courses FOREIGN KEY (CourseKey)
        REFERENCES Courses(CourseKey)
);
CREATE INDEX IX_Lessons_Course_Ord           -- ~ idx_lessons_course_order
    ON Lessons (CourseKey, Ord);
GO

CREATE TABLE Quizzes (                      -- ~ collection quizzes
    QuizKey   VARCHAR(60) NOT NULL,
    LessonKey VARCHAR(60) NOT NULL,
    CONSTRAINT PK_Quizzes PRIMARY KEY (QuizKey),
    CONSTRAINT FK_Quizzes_Lessons FOREIGN KEY (LessonKey)
        REFERENCES Lessons(LessonKey)
);
CREATE INDEX IX_Quizzes_Lesson ON Quizzes (LessonKey);
GO

-- Tách từ mảng Quiz.Questions — bước chuẩn hoá 1NF.
-- Phải sinh khoá thay thế QuestionId; bên ArangoDB không cần vì câu hỏi
-- lồng trong tài liệu quiz và luôn được đọc trọn vẹn cùng quiz.
CREATE TABLE QuizQuestions (
    QuestionId  INT IDENTITY(1,1) NOT NULL,
    QuizKey     VARCHAR(60)   NOT NULL,
    Ord         INT           NOT NULL,
    Q           NVARCHAR(500) NOT NULL,
    OptionA     NVARCHAR(200) NOT NULL,
    OptionB     NVARCHAR(200) NOT NULL,
    OptionC     NVARCHAR(200) NOT NULL,
    OptionD     NVARCHAR(200) NOT NULL,
    AnswerIndex INT           NOT NULL
        CONSTRAINT CK_QQ_Answer CHECK (AnswerIndex BETWEEN 0 AND 3),
    CONSTRAINT PK_QuizQuestions   PRIMARY KEY (QuestionId),
    CONSTRAINT UQ_QuizQuestions_Ord UNIQUE (QuizKey, Ord),
    CONSTRAINT FK_QQ_Quizzes FOREIGN KEY (QuizKey)
        REFERENCES Quizzes(QuizKey) ON DELETE CASCADE
);
GO

-- ===========================================================================
-- 2. Bảng kết  (~ 4 edge collection)
--    Bốn bảng này tồn tại chỉ để biểu diễn quan hệ nhiều–nhiều. Trong
--    property graph chúng LÀ chính bản thân edge collection (Mục 2.2).
-- ===========================================================================
CREATE TABLE Enrollments (                  -- ~ edge enrolled_in
    UserKey    VARCHAR(20) NOT NULL,
    CourseKey  VARCHAR(40) NOT NULL,
    EnrolledAt DATETIME2   NOT NULL,
    -- Progress là DỮ LIỆU DẪN XUẤT (tính lại được từ Completions + Lessons).
    -- Giữ nguyên phi chuẩn hoá này ở CẢ HAI hệ là điều kiện để so sánh công
    -- bằng: bên ArangoDB nó cũng là thuộc tính của cạnh enrolled_in.
    Progress   INT         NOT NULL
        CONSTRAINT DF_Enr_Progress DEFAULT 0
        CONSTRAINT CK_Enr_Progress CHECK (Progress BETWEEN 0 AND 100),
    CONSTRAINT PK_Enrollments PRIMARY KEY (UserKey, CourseKey),
    CONSTRAINT FK_Enr_Users   FOREIGN KEY (UserKey)   REFERENCES Users(UserKey),
    CONSTRAINT FK_Enr_Courses FOREIGN KEY (CourseKey) REFERENCES Courses(CourseKey)
);
-- Chiều nghịch — BẮT BUỘC để ngang bằng edge index của ArangoDB (nguyên tắc N3).
-- Bỏ chỉ mục này sẽ tạo kết quả có lợi cho ArangoDB một cách giả tạo.
CREATE INDEX IX_Enrollments_Course_User ON Enrollments (CourseKey, UserKey);
-- Phục vụ CTE MyCompleted trong Q2-SQL
CREATE INDEX IX_Enrollments_User_Progress ON Enrollments (UserKey, Progress)
    INCLUDE (CourseKey);
GO

CREATE TABLE Completions (                  -- ~ edge completed
    UserKey     VARCHAR(20) NOT NULL,
    LessonKey   VARCHAR(60) NOT NULL,
    CompletedAt DATETIME2   NOT NULL,
    Score       INT         NULL
        CONSTRAINT CK_Comp_Score CHECK (Score IS NULL OR Score BETWEEN 0 AND 100),
    CONSTRAINT PK_Completions PRIMARY KEY (UserKey, LessonKey),
    CONSTRAINT FK_Comp_Users   FOREIGN KEY (UserKey)   REFERENCES Users(UserKey),
    CONSTRAINT FK_Comp_Lessons FOREIGN KEY (LessonKey) REFERENCES Lessons(LessonKey)
);
CREATE INDEX IX_Completions_Lesson ON Completions (LessonKey);
GO

CREATE TABLE Prerequisites (                -- ~ edge prerequisite_of
    PrereqCourseKey VARCHAR(40) NOT NULL,   -- ~ _from : khóa phải học TRƯỚC
    CourseKey       VARCHAR(40) NOT NULL,   -- ~ _to   : khóa học SAU
    -- CourseKey đứng TRƯỚC trong khoá chính: Q2 và Q3 đều tra theo
    -- "cho khóa này, tìm các tiên quyết của nó". ArangoDB không có quyết định
    -- tương ứng vì edge index đánh chỉ mục cả hai chiều (gánh nặng thiết kế).
    CONSTRAINT PK_Prerequisites PRIMARY KEY (CourseKey, PrereqCourseKey),
    CONSTRAINT FK_Pre_Course FOREIGN KEY (CourseKey)
        REFERENCES Courses(CourseKey),
    CONSTRAINT FK_Pre_Prereq FOREIGN KEY (PrereqCourseKey)
        REFERENCES Courses(CourseKey),
    -- Chặn cạnh tự vòng. Bên ArangoDB KHÔNG có ràng buộc tương ứng —
    -- một cạnh với _from == _to được chấp nhận.
    CONSTRAINT CK_Pre_NoSelf CHECK (CourseKey <> PrereqCourseKey)
);
CREATE INDEX IX_Prerequisites_Prereq ON Prerequisites (PrereqCourseKey);
GO

CREATE TABLE Ratings (                      -- ~ edge rated
    UserKey   VARCHAR(20)   NOT NULL,
    CourseKey VARCHAR(40)   NOT NULL,
    Stars     INT           NOT NULL
        CONSTRAINT CK_Rat_Stars CHECK (Stars BETWEEN 1 AND 5),
    Comment   NVARCHAR(500) NULL,
    CONSTRAINT PK_Ratings PRIMARY KEY (UserKey, CourseKey),
    CONSTRAINT FK_Rat_Users   FOREIGN KEY (UserKey)   REFERENCES Users(UserKey),
    CONSTRAINT FK_Rat_Courses FOREIGN KEY (CourseKey) REFERENCES Courses(CourseKey)
);
CREATE INDEX IX_Ratings_Course ON Ratings (CourseKey) INCLUDE (Stars);
GO

PRINT 'Lược đồ StudyGraphSql đã tạo: 10 quan hệ (8 collection bên ArangoDB + 2 bảng tách từ 1NF).';
PRINT 'Bước tiếp theo: cd StudyGraph.SqlImporter && dotnet run';
GO
