# Disl — v0.1 Design Spec (v12)

> **Disl** *(pronounced "diesel")* — Dalmurii's IR Style Language.
> An IR-flavored systems language: explicit control flow with block parameters,
> manual memory management, small specification.
> Designed for compiler backends, IR research, and small system tools.

---

## 1. 정체성

### 1.1. 위치

- **C 와 비슷한 영역, 더 명시적** — control flow 가 explicit (block parameter, 명시 jump)
- **LLVM IR 보다 친화적** — type system, generic, sugar
- **고유 위치:** "LLVM IR 의 사람-친화 버전"

### 1.2. IR 가치 정의

**IR 가치 = SSA + 명시 control flow + 명시 메모리 + 비용 visibility**

이 가치들이 *Disl 의 핵심*. 다음 features 가 IR 가치 *유지*:

- **SSA** (block parameter)
- **Block + branch + jump** (명시 control flow)
- **Load (`:=`), store (`=`), alloca** (명시 메모리)
- **모든 함수 호출 visible** (비용 명시)
- **Sigil** (`%`, `#` — 시각 분류)

다음 features 가 *IR 가치 깨지 않음* (compile time, runtime 비용 0):

- **Generic** (monomorphization)
- **Method call sugar** (정적 dispatch, 정적 resolution)
- **Namespace** (이름 organize)
- **Self keyword** (substitution)
- **Inline terminator** (block boilerplate 회피)
- **Array literal sugar** (initialization)

다음 features 가 *IR 가치 깸* (Disl 거부):

- ❌ **Mutable variable** (SSA 깸)
- ❌ **For/while loop** (block parameter 흐름 숨김)
- ❌ **Defer** (cleanup 자동, 비용 hidden)
- ❌ **Exception** (control flow 숨김)
- ❌ **가상 dispatch** (비용 hidden)
- ❌ **Operator overloading** (함수 호출 비용 hidden)
- ❌ **Closure** (capture 메커니즘 hidden)
- ❌ **Async** (state machine hidden)
- ❌ **Macro** (코드 생성 hidden)

**핵심:** Disl 가 *IR 의 모든 가치 + 현대 언어의 작은 sugar*. CLIF 가 IR 만 (사용자 친화 안 추구), Rust 가 추상화 풍부 (IR 가치 약함), Disl 이 *둘 다*.

### 1.3. 영역

**Fit:**

- 컴파일러 백엔드 (LLVM 대안)
- IR 학습/연구
- 작은 시스템 도구
- 알고리즘 (수십~수백 줄)
- 작은~중간 자료구조

**Limit:**

- 일반 애플리케이션 (verbose 부담)
- 큰 프로그램 (수만 줄)
- 풍부한 추상화 필요한 코드

### 1.4. 디자인 원칙

1. **명시성 우선** — 모든 control flow, 메모리, 변환 visible
2. **작은 사양** — 핵심 키워드 ~15, 사용자가 한 번 학습
3. **비용 visibility** — 모든 함수 호출, allocation, dispatch 명시
4. **단일 스레드 v0.1** — multithreading 은 v0.5+
5. **Compiler core 최소** — operations 은 stdlib prelude 가 정의

### 1.5. Trade-off Framework: "어느 게 더 Poison?"

각 디자인 결정이 *trade-off*. 양쪽 비용 평가, *덜 poison* 한 옵션 선택.

**평가 기준:**

- 사용자 부담 (verbose, 학습)
- 컴파일러 작업
- 정체성 일관 (영역, 철학)
- 2026 년 언어 표준

**예시 결정:**

| 결정       | 옵션 A                  | 옵션 B                   | 선택 (덜 poison)                             |
|----------|-----------------------|------------------------|-------------------------------------------|
| Generic  | 없음 (mangling 회피)      | 있음 (mangling 필요)       | **B** — generic 없는 건 2026 년에 outdated     |
| Loop     | block parameter only  | for/while 도입           | **A** — for/while 가 정체성 약화, sugar 압력      |
| Method   | prefix convention     | namespace + sugar      | **B** — `#obj` 어차피 visible, sugar 가 짧고 자연 |
| Atomic   | v0.1 도입               | v0.5+ 연기               | **B** — v0.1 도입이 큰 timeline 위협            |
| SSA      | non-SSA (mutable var) | SSA (block parameter)  | **B** — Disl 영역 (시스템) 에서 명시성 우선           |
| Mangling | 복잡 encoding           | LLVM quoted identifier | **B** — 단순, 사용자 친화                        |

**의사소통 가치:**

이 framework 이 *결정 이유 명확*. "X 가 더 poison 이라 Y 선택." 정직.

영역에 따라 *다른 결정* — RazorForge (애플리케이션) 가 풍부 sugar, Disl (시스템) 이 명시성. 같은 framework, 다른 영역, 다른 답.

---

## 2. Token 세트

### 2.1. 키워드 (15)

**Block-level (12):**

- `routine`, `block`, `struct`, `enum`, `const`
- `jump`, `branch`, `select`, `branch_table`, `return`
- `unreachable`, `alloca`

**Literal (3):**

- `true`, `false`, `null`

**Note:** `trap` is *not* a keyword — it is a `@noreturn` routine in the
prelude (lowers to `call llvm.trap()`), invoked with the usual call syntax
(`trap()`). It can appear as an inline terminator after `?` or `->`
because the call is `@noreturn`. `unreachable` is a true keyword (a pure
optimizer hint that emits no code) and is written without parentheses.

### 2.2. 연산자 (9)

**산술:**

- `+`, `-`, `*`

**비트:**

- `&`, `|`, `^`, `~`

**비교:**

- `==`, `!=`

### 2.3. Sigil (3)

- `%` — value (SSA 변수)
- `#` — pointer
- `@` — attribute marker

### 2.4. 구두점

- 괄호: `(`, `)`, `[`, `]`, `{`, `}`
- 분리: `,`, `:`, `;`, `::`
- 접근: `.` (field, method)
- Memory: `=`, `:=`
- Branch: `?`, `<`, `>`, `->`, `_`

### 2.5. 거부된 syntax

- `<`, `>`, `<=`, `>=` (signedness 모호 — 함수로)
- `<<`, `>>` (generic 충돌 — 함수로)
- `/`, `%` (이전 signedness 모호, 이제 unsigned 없으니 도입 가능 but 일관성 위해 함수)
- `&&`, `||` (short-circuit 복잡 — v0.5+)

**대신:** `slt`, `sgt`, `sdiv`, `srem`, `shl`, `ashr` 등이 *prelude 의 external LLVM 함수*.

---

## 3. Type System

### 3.1. 기본 type

**정수 (signed only):**

- `i8`, `i16`, `i32`, `i64`

**실수:**

- `f32`, `f64`

**기타:**

- `bool` — true/false (1 byte)
- `void` — 반환값 없음
- `char` — 1 byte, distinct type (ASCII)
- `iaddr` — 포인터 크기 정수 (CPU 따라 i32 또는 i64)

**Type-level signedness 없음:**

- `u8`, `u16`, `u32`, `u64` 도입 안 함
- 정수 type 이 *비트 패턴*. signedness 없음.
- Operation 이 signed/unsigned 의미 결정 (LLVM IR 모델)
- 예: `slt(%a, %b)` signed 비교, `ult(%a, %b)` unsigned 비교

### 3.2. 포인터

- `ptr` — opaque (raw 메모리, 미사용 권유)
- `ptr<T>` — typed pointer
- `null` — typed null literal

### 3.3. Array

- `array<T, N>` — fixed-size array, N 은 컴파일 타임 상수
- 다차원: `array<array<T, M>, N>`

### 3.4. Struct

```
struct Vector3D
    x: f32
    y: f32
    z: f32
```

Generic:

```
struct List<T>
    data: ptr<T>
    length: i64
    capacity: i64
```

### 3.5. Enum (C-style, distinct type)

```
enum TokenKind: i32
    EOF = 0
    NUMBER = 1
    PLUS = 2
    MINUS = 3
```

또는 자동 할당:

```
enum Color
    Red
    Green
    Blue
```

**규칙:**

- Default underlying type `i32`, 명시 가능 (`enum Name: i8`)
- Distinct type (정수와 자유 변환 거부, bitcast 명시)
- Namespace: `Name::Variant`
- `branch_table` 와 자연스러운 통합

### 3.6. Callable

```
callable<@callconv("c"), (i32, i64), bool>
```

- 첫 인자: calling convention attribute
- 둘째: 인자 type tuple `(T1, T2, ...)`
- 셋째: 반환 type
- 단일 인자 tuple: `(T,)` (trailing comma)

### 3.7. String (stdlib)

```
struct String
    data: ptr<char>
    length: i64
    capacity: i64
```

- byte sequence (UTF-8 capable)
- `List<char>` 와 비슷한 패턴
- Allocator 받음

**String literal:**

- `"hello"` → `ptr<char>` + length 별도 명시
- `"한글"` 가능 (UTF-8 byte sequence)
- Length 자동 추론 안 함 (v0.1)

---

## 4. Sigils

- `%` — value (SSA 변수)
- `#` — pointer

**규칙:** Type 이 sigil 결정.

```
%v: i32 = 42                   ; value
#p: ptr<i32> = alloca<i32>     ; pointer
```

---

## 5. Memory Operations

### 5.1. Load / Store

- `:=` — load (메모리 → value)
- `=` — store (value → 메모리) 또는 binding

**LHS sigil 으로 의미 구분:**

```
%v: i32 := #p          ; load (RHS 가 ptr)
#p = %v                ; store (LHS 가 ptr)
%x: i32 = 42           ; binding (LHS 가 %)
```

### 5.2. Allocation

```
#x: ptr<T> = alloca<T>                    ; uninit
#arr: ptr<array<T, 8>> = alloca<array<T, 8>>

; sugar: 초기화
#arr: ptr<array<i32, 5>> = alloca<array<i32, 5>>([1, 2, 3, 4, 5])
```

**Array literal sugar:**

- `[v1, v2, ...]` — element 명시
- Element 수가 array 크기와 일치 (mismatch = 컴파일 에러)
- Element type 추론 또는 명시
- 다차원 nested: `[[1,2,3], [4,5,6]]`

### 5.3. Field / Index Access (sugar)

```
%v.x                   ; struct field 접근 → extract
#p.x                   ; pointer 의 field → elementptr
#arr[i]                ; pointer indexing → elementptr
#arr[i] = v            ; index store → elementptr + store
%v: T := #arr[i]       ; index load
```

### 5.4. Heap Allocation

`alloca` 는 stack 만. Heap 은 외부 함수 + Allocator 패턴.

---

## 6. Control Flow

### 6.1. Block

```
block name(%a: i32, %b: i64):
    ; statements
    ; ends with terminator
```

- 4 space indentation (convention, not grammar)
- Block parameter 가 cross-block dataflow (replaces phi)
- 빈 매개변수: `block name():`

### 6.2. Terminators

**`jump target(args)`** — 무조건 분기

```
jump loop(0, 100)
```

**`branch %cond ? then_target : else_target`** — 이분 분기

각 target 자리에 *block 호출 또는 inline terminator* 가능:

```
branch %is_done ? exit() : continue(%i)
branch %is_inbounds ? access() : trap()       ; @noreturn routine call
branch %known_true ? continue() : unreachable ; inline unreachable
branch %is_error ? return -1 : do_work()      ; inline return
```

**Inline terminator 종류:**

- `unreachable` — 도달 불가 (옵티마이저 hint, 키워드)
- `return` 또는 `return %value` — 함수 종료 (키워드)
- `@noreturn` 루틴 호출 (예: `trap()`) — 함수가 돌아오지 않음을 *시그니처* 가
  보장하면 inline terminator 자리에 그대로 호출 가능

이거 흔한 패턴 (bounds check, null check, early return) *block 분리 회피*.

**`select`** — cascade (boolean expressions)

각 분기 자리에 block 호출 또는 inline terminator:

```
select:
    is_digit(%c) -> read_num()
    %c == '+' -> handle_plus()
    %c == '-' -> handle_minus()
    _ -> default()

; inline terminator 도 가능
select:
    %ok -> continue()
    %is_overflow -> trap()
    _ -> return -1
```

**`branch_table %value:`** — jump table (value equality)

각 case 자리에 block 호출 또는 inline terminator:

```
branch_table %op:
    1 -> add_op()
    2 -> sub_op()
    3 -> mul_op()
    _ -> default()

; exhaustive switch — default unreachable
branch_table %tag:
    0 -> handle_zero()
    1 -> handle_one()
    2 -> handle_two()
    _ -> unreachable
```

**`return value`** — 함수 종료

```
return %result
return                 ; void 반환
```

**`unreachable`** — 도달 불가 표시 (옵티마이저 정보)

Block terminator 또는 *branch/select/branch_table 의 inline target* 으로:

```
; standalone
block dead_code():
    unreachable

; inline (자주 사용)
select:
    %tag == 1 -> handle_a()
    %tag == 2 -> handle_b()
    _ -> unreachable

branch %known_true ? continue() : unreachable
```

**`trap()`** — 의도적 crash (prelude `@noreturn` routine, *not* a keyword)

`trap` 은 prelude 의 `@noreturn` 루틴. 일반 호출 자리뿐 아니라 *block
terminator* 또는 *branch/select/branch_table 의 inline target* 으로도
사용 가능 — `@noreturn` 시그니처가 호출 후 실행이 이어지지 않음을 보증.

```
; standalone
block abort_block():
    trap()

; inline (자주 사용)
%bounds_ok: bool = ult(%i, %len)
branch %bounds_ok ? continue() : trap()

select:
    %is_valid -> process()
    %is_overflow -> trap()
    _ -> handle_other()
```

### 6.3. Value Select

```
%v: T = %cond ? %a : %b
```

- 양쪽 expression 모두 평가 (LLVM 스타일, short-circuit 아님)
- Side effect 있는 호출 권유 안 함
- Branchless 코드 (옵티마이저가 cmov 생성 가능)

```
%abs: i32 = slt(%x, 0) ? neg(%x) : %x
%max: i32 = sgt(%a, %b) ? %a : %b
```

### 6.4. Loop

**키워드 없음.** Block parameter loop:

```
jump loop(0, 0)

block loop(%i: i64, %sum: i64):
    %done: bool = sge(%i, 100)
    branch %done ? exit(%sum) : body(%i, %sum)

block body(%i: i64, %sum: i64):
    %new_sum: i64 = %sum + %i
    %next: i64 = %i + 1
    jump loop(%next, %new_sum)

block exit(%sum: i64):
    return %sum
```

**Mutable state (필요 시):** 포인터 + alloca:

```
#counter: ptr<i64> = alloca<i64>
#counter = 0
; ... block 안에서 load/store
```

---

## 7. Operations

### 7.1. Sugar 연산자

**산술 (signedness-irrelevant):**

- `+`, `-`, `*`

**비트 (signedness-irrelevant):**

- `&`, `|`, `^`, `~`

**비교 (signedness-irrelevant):**

- `==`, `!=`

### 7.2. Prelude 함수 (external LLVM)

이전 키워드들이 *prelude 의 external LLVM 함수*. Compiler core 안 알아야 함.

**비교:**

```
@[external("llvm"), template("{result} = icmp slt {T} {a}, {b}")]
routine slt<T>(%a: T, %b: T) -> bool

@[external("llvm"), template("{result} = icmp sgt {T} {a}, {b}")]
routine sgt<T>(%a: T, %b: T) -> bool

; sle, sge — signed
; ult, ugt, ule, uge — unsigned (같은 비트 패턴, 다른 의미)
; fcmp 등 — float
```

같은 정수 값에 *signed 또는 unsigned 의미* 적용 가능. C 의 type-level signedness 없이도 모든 비교 표현.

**나눗셈:**

```
@[external("llvm"), template("{result} = sdiv {T} {a}, {b}")]
routine sdiv<T>(%a: T, %b: T) -> T

@[external("llvm"), template("{result} = srem {T} {a}, {b}")]
routine srem<T>(%a: T, %b: T) -> T

; udiv, urem — unsigned 의미
```

**Shift:**

```
@[external("llvm"), template("{result} = shl {T} {a}, {b}")]
routine shl<T>(%a: T, %b: T) -> T

@[external("llvm"), template("{result} = ashr {T} {a}, {b}")]
routine ashr<T>(%a: T, %b: T) -> T

@[external("llvm"), template("{result} = lshr {T} {a}, {b}")]
routine lshr<T>(%a: T, %b: T) -> T
```

**변환:**

```
@[external("llvm"), template("{result} = sext {T1} {x} to {T2}")]
routine sext<T1, T2>(%x: T1) -> T2

@[external("llvm"), template("{result} = zext {T1} {x} to {T2}")]
routine zext<T1, T2>(%x: T1) -> T2

@[external("llvm"), template("{result} = trunc {T1} {x} to {T2}")]
routine trunc<T1, T2>(%x: T1) -> T2

@[external("llvm"), template("{result} = bitcast {T1} {x} to {T2}")]
routine bitcast<T1, T2>(%x: T1) -> T2

; fptosi, sitofp 등
```

**Float:**

```
@[external("llvm"), template("{result} = fadd {T} {a}, {b}")]
routine fadd<T>(%a: T, %b: T) -> T

; fsub, fmul, fdiv, feq, flt 등
```

**Bit builtin:**

```
@[external("llvm"), template("{result} = call {T} @llvm.ctpop.{T}({T} {x})")]
routine popcount<T>(%x: T) -> T

; clz, ctz, bswap 등
```

이 모든 게 *stdlib prelude*. 자동 import. Compiler 안 수정하고 *새 intrinsic 추가 가능*.

### 7.3. Char 산술

```
%c: char = '5'
%v: i8 = %c - '0'              ; char - char = i8
```

- char 끼리 산술 → integer 결과
- char 와 정수 변환 → bitcast 명시

### 7.4. 거부된 sugar

- `/`, `%` — 함수 (`sdiv`, `srem`) 로
- `<`, `>`, `<=`, `>=` — 함수 (`slt`, `sgt`, `sle`, `sge`) 로
- `<<`, `>>` — 함수 (`shl`, `ashr`, `lshr`) 로
- `&&`, `||` — short-circuit 복잡 (v0.5+ 옵션)

---

## 8. Literal

### 8.1. 정수

```
42, -5, 0xFF, 0b1010, 0o17
```

- 다형 type (context 추론)

### 8.2. Float

```
3.14, 1.5e10, 0.5f
```

### 8.3. Boolean

```
true, false
```

### 8.4. Char

```
'A', '\n', '\t', '\0'
'\xFF'                 ; hex escape
'\u{D55C}'             ; Unicode escape (codepoint, but stored as multi-byte UTF-8)
```

- ASCII char literal (1 byte)
- Multi-byte char literal `'한'` 거부

### 8.5. String

```
"hello", "한글", "\n"
```

- byte sequence (UTF-8 capable)
- `ptr<char>` + length 명시

### 8.6. Array

```
[1, 2, 3, 4, 5]
[[1,2,3], [4,5,6]]
```

- `alloca<array<...>>` 의 인자에서 사용
- Element 수 + type 명시 또는 추론

### 8.7. Struct

```
Vector3D { x = 1.0, y = 2.0, z = 3.0 }
```

### 8.8. Null

```
null                   ; typed null
```

### 8.9. `const` (compile-time constant)

```
const NODE_KEYS: i64 = 7
const NODE_CHILDREN: i64 = NODE_KEYS + 1
const RESULT_ALIGN: i64 = max(alignof<i64>(), alignof<f64>())
```

규칙:

- File-scope declaration. `const NAME: TYPE = EXPR` 형태.
- `EXPR` 가 *반드시 comptime-reducible* — 정수/부울 산술, 다른 `const`, 그리고
  prelude 의 `sizeof<T>()`, `alignof<T>()`, `max(...)`, `min(...)` 같은
  comptime intrinsic 만 허용. 일반 routine 호출 거부.
- 사용 가능 위치: `array<T, N>` 의 N, `@aligned(N)`, generic argument,
  enum value, 다른 const, value position (comptime → runtime 자동 substitution).
- Compile time 에 *값으로 펴짐* — runtime 변수 아님, address-of 불가.
- 비-comptime 표현이 RHS 에 등장하면 컴파일 에러.

이 좁은 comptime 만이 v0.1 에서 허용 — `comptime` 블록, 일반 함수의 comptime
호출, 사용자 정의 comptime 함수 등은 **거부 유지** (§13.5). `const` 가 type-level
정수가 필요한 곳 (array size, alignment) 의 *유일한* 진입점.

---

## 9. Function

### 9.1. 정의

**Free function:**

```
routine name<T1, T2>(%a: T1, #b: ptr<T2>) -> T2
    block entry:
        ; ...
        return ...
```

**Type-associated routine (namespace):**

```
struct BTreeList<T, N>
    ; ...

routine BTreeList<T, N>::new(#alloc: ptr<Allocator>) -> BTreeList<T, N>
    ; ...

routine BTreeList<T, N>::remove(#self: ptr<Self>, %i: i64, #alloc: ptr<Allocator>) -> T
    ; Self = BTreeList<T, N>
    ; ...
```

- `Type<...>::method` namespace
- `Self` keyword (type substitution)
- 첫 인자가 `#self: ptr<Self>` 또는 `%self: Self` 면 *method call 가능*
- 정적 dispatch (compile time resolution)

**Generic 매개변수:**

- `<T>` — type parameter
- 호출 시 추론 또는 명시
- Type-associated method 의 generic 은 *Self 로부터 추론* 가능

### 9.2. 호출

**Free function:**

```
%result: i32 = my_func(%a, #b)
%result: T = generic_func<T>(%x)    ; 명시
%result: T = generic_func(%x)       ; 추론
```

**Type-associated (명시적 namespace):**

```
%list: BTreeList<i32, 8> = BTreeList<i32, 8>::new(#alloc)
%v: i32 = BTreeList<i32, 8>::remove(#list_ptr, 0, #alloc)
```

**Method call sugar:**

```
%v: i32 = #list_ptr.remove(0, #alloc)        ; ptr<Self> method
%magnitude: f32 = %vec.magnitude()           ; Self method (value)
```

- `#obj.method(args)` → `Type::method(#obj, args)`
- `%obj.method(args)` → `Type::method(%obj, args)`
- Compile time resolution (정적 dispatch)
- Type 추론 from `#obj` / `%obj`

**규칙:**

- Sugar 는 *짧음* 위해, 의미 같음
- 모든 인자 visible (`#obj` 가 첫 인자)
- 가상 dispatch 없음 (vtable 없음)
- 상속 없음, 캡슐화 없음

### 9.3. External (FFI / Intrinsic)

**C FFI:**

```
@[external("c"), callconv("c"), symbol("malloc")]
routine c_malloc(%size: i64) -> ptr
```

**LLVM intrinsic:**

```
@[external("llvm"), template("{result} = bitcast {T1} {x} to {T2}")]
routine bitcast<T1, T2>(%x: T1) -> T2
```

**Template variables:**

- `{result}` — output value name
- `{T}`, `{T1}`, `{T2}` — generic type parameters
- `{x}`, `{a}`, `{b}` — argument names

Compiler 가 template 에 *substitution 적용*. LLVM IR 또는 다른 backend 코드 생성.

### 9.4. Calling Convention

```
@callconv("c")           ; C ABI
@callconv("fast")        ; LLVM fast
@callconv("cold")        ; rarely called
```

문자열 기반 (LLVM 에 passthrough).

---

## 10. Attributes

### 10.1. Syntax

```
@name                  ; 단독
@name(args)            ; 인자
@[a, b, c]             ; 묶기
@[a(x), b, c(y)]       ; 묶기 + 인자
```

### 10.2. ABI Attributes

- `@callconv("name")` — calling convention
- `@variadic` — variadic function
- `@zeroext`, `@signext` — argument extension
- `@aligned(N)` — alignment

### 10.3. Behavioral Attributes

- `@noreturn` — never returns
- `@nounwind` — no exception
- `@pure` — no side effects, deterministic
- `@readonly` — no writes
- `@inline` — inlining hint

### 10.4. External Attributes

- `@external("backend")` — `"c"` 또는 `"llvm"`
- `@symbol("name")` — exact symbol name (C linking)
- `@template("...")` — backend code template

---

## 11. Symbol Names

Generic, namespace, method 가 있어 *symbol name 에 특수 문자* (`<`, `>`, `,`, `:`) 등장. 근데 *복잡 mangling 불필요* — LLVM IR 의 *quoted
identifier* 활용.

**규칙:**

```
Disl 이름: List<i32>::push
LLVM IR symbol: @"_D::List<i32>::push"
Object file symbol: _D::List<i32>::push
```

- Source name 을 *그대로* symbol 로
- LLVM IR 의 quoted identifier (`@"..."`) 사용
- Prefix `_D::` 로 Disl symbol 식별
- 모든 ASCII 문자 가능 (현대 linker, ELF/Mach-O/COFF 모두 지원)

**예시:**

```
my_func              → @"_D::my_func"
my_func<i32>         → @"_D::my_func<i32>"
List<i32>            → type
List<i32>::push      → @"_D::List<i32>::push"
BTreeList<i32, 8>::remove → @"_D::BTreeList<i32, 8>::remove"
```

**왜 단순한가:**

C++ 의 mangling 복잡함의 이유 — *역사적 (1980 년대 linker 제약)*. 현대 (2026):

- LLVM IR 의 quoted identifier (`@"..."`)
- 현대 linker 가 임의 byte sequence 지원
- Binary 크기 부담 작음

C++ 시대의 *encoding 부담* 회피. Source name 그대로.

**C 라이브러리 통합:**

C 함수는 mangling 없음, plain symbol:

```
@[external("c"), symbol("malloc")]
routine c_malloc(%size: i64) -> ptr
```

LLVM IR:

```llvm
declare ptr @malloc(i64)         ; plain, C symbol
```

`@symbol("malloc")` 이 *exact name*. C ABI 직접.

**Disl → C 호환:**

```
@[export("my_func")]
routine my_func(%x: i32) -> i32
    ; ...
```

`@export` 가 *plain symbol* 추가:

```llvm
define i32 @"_D::my_func"(i32 %x) { ... }
@my_func = alias i32 (i32), ptr @"_D::my_func"
```

C 가 `my_func` 호출 가능 (plain name).

**규칙 명시적:**

- `@external("c")` — plain symbol, C ABI
- 일반 routine — `_D::` prefix + Disl name (quoted in LLVM IR)
- `@export("name")` — plain symbol 추가 (alias)

**사용자 친화:**

Profiler / debugger 출력:

```
99.5%  _D::List<i32>::push
0.3%   _D::BTreeList<i32, 8>::remove
```

*Disl source name 그대로*. Demangle 도구 *불필요*. 사용자가 바로 인식.

비교 — C++ mangling:

```
99.5%  _ZN4ListIiE4pushEi
```

읽기 불가능, `c++filt` 필요.

**호환성:**

ELF (Linux), Mach-O (macOS), COFF (Windows) 모두 *임의 byte sequence symbol* 지원. Quoted identifier 호환. 만약 호환 issue 있으면
*fallback mangling* 옵션 (v0.5+).

---

## 12. Allocator Pattern

수동 vtable (interface 없음):

```
struct Allocator
    alloc_fn: callable<@callconv("c"), (ptr, i64), ptr>
    realloc_fn: callable<@callconv("c"), (ptr, ptr, i64), ptr>
    free_fn: callable<@callconv("c"), (ptr, ptr), void>
    state: ptr

routine alloc<T>(#alloc: ptr<Allocator>, %count: i64) -> ptr<T>
    block entry:
        %size: i64 = sizeof<T> * %count
        %raw: ptr = #alloc.alloc_fn(#alloc.state, %size)
        %typed: ptr<T> = bitcast<ptr, ptr<T>>(%raw)
        return %typed

routine free<T>(#alloc: ptr<Allocator>, #ptr: ptr<T>) -> void
    block entry:
        %raw: ptr = bitcast<ptr<T>, ptr>(#ptr)
        #alloc.free_fn(#alloc.state, %raw)
        return
```

**규칙:**

- Allocation 하는 함수는 *allocator 받음*
- 함수 시그니처가 *비용 visibility*
- `make_heap_allocator()` — 기본 libc 기반

---

## 13. 도입 안 하는 것 (v0.1)

### 13.1. Type

- ❌ Unsigned (`u8`, `u16`, `u32`, `u64`) — signed 충분, C 함정 회피

### 13.2. OOP

- ✓ **Namespace + method call sugar** (위 9.1, 9.2 참조) — 정적 dispatch
- ❌ Operator overloading
- ❌ Inheritance
- ❌ Virtual dispatch (vtable)
- ❌ Encapsulation (private fields)
- ✓ Concept constraints (compile-time required routine sets)

**도입한 것:**

- `Type<T>::method` namespace
- `#obj.method()` 호출 sugar (정적, vtable 없음)
- `Self` keyword
- `concept` / `require` constraints for generic algorithms

**거부한 것 (OOP 메커니즘):**

- 가상 dispatch — Disl 는 *vtable 패턴* (수동) 사용
- 상속 — composition 또는 wrapper struct 로
- 캡슐화 — 모든 필드 public
- 연산자 overloading — 비용 hidden 회피

### 13.2.1. Concept Constraints

Concepts are compile-time contracts, not runtime interfaces. A concept lists
associated routines that an `Ops` type must provide.

```disl
concept Compare<T, Ops>
    routine Ops::compare(%a: T, %b: T) -> i32

concept LazySegmentOps<T, L, Ops>
    routine Ops::combine(%left: T, %right: T) -> T
    routine Ops::apply(%value: T, %lazy: L, %length: i64) -> T
    routine Ops::compose(%newer: L, %older: L) -> L
    routine Ops::identity_value() -> T
    routine Ops::identity_lazy() -> L
    routine Ops::is_identity_lazy(%lazy: L) -> bool
```

Rules:

- Concept satisfaction is checked at compile time.
- Concepts do not create vtables or dynamic dispatch.
- Required routines use the same associated-routine identity as normal code.
- Generic algorithms can name an `Ops` type and require a concept.
- Satisfaction is structural — a type satisfies a concept by having the
  required routines. There is no `impl` block; nothing is opted into.

### 13.3. Control Flow

- ❌ `for`, `while` — block parameter loop 사용
- ❌ `defer` — 명시적 cleanup
- ❌ Exception / try-catch — 명시적 error 반환
- ❌ Coroutine / async — 단일 스레드
- ❌ `goto` — `jump` 가 같은 역할

### 13.4. Threading

- ❌ Atomic operations
- ❌ Mutex
- ❌ Threading primitives
- ❌ Memory model

**v0.5+ 작업 (11-15 주):**

- Atomic<T> type
- Memory ordering
- Threading primitives
- Mutex (futex-based 또는 spinlock)

### 13.5. 기타

- ❌ Comptime (Zig 스타일)
- ❌ Macro 시스템 (v0.5+)
- ❌ `byte` 별도 type — `i8` 또는 `char` 가 그 역할
- ❌ 변수 mutation — SSA + pointer 패턴
- ❌ `declare` 키워드 — `routine + @external` 로 통합

---

## 14. 입문 예제

```
; ============================================================
; External (C FFI)
; ============================================================

@[external("c"), callconv("c"), symbol("printf")]
routine c_printf(%fmt: ptr<char>, %x: i64) -> i32

@[external("c"), callconv("c"), symbol("printf")]
routine c_printf_str(%fmt: ptr<char>, %s: ptr<char>) -> i32

; ============================================================
; Section 1: Binary Search
; ============================================================

routine binary_search(#arr: ptr<i32>, %length: i64, %target: i32) -> i64
    block entry:
        jump loop(0, %length)

    block loop(%lo: i64, %hi: i64):
        %done: bool = sge(%lo, %hi)
        branch %done ? not_found() : check(%lo, %hi)

    block check(%lo: i64, %hi: i64):
        %diff: i64 = %hi - %lo
        %half: i64 = sdiv(%diff, 2)
        %mid: i64 = %lo + %half
        %v: i32 := #arr[%mid]
        select:
            %v == %target -> found(%mid)
            slt(%v, %target) -> loop_higher(%mid, %hi)
            _ -> loop(%lo, %mid)

    block loop_higher(%mid: i64, %hi: i64):
        %new_lo: i64 = %mid + 1
        jump loop(%new_lo, %hi)

    block found(%i: i64):
        return %i

    block not_found():
        return -1

; ============================================================
; Section 2: Caesar Cipher
; ============================================================

routine caesar_shift(%c: char, %shift: i32) -> char
    block entry:
        %is_upper_lo: bool = sge(%c, 'A')
        %is_upper_hi: bool = sle(%c, 'Z')
        %is_upper: bool = %is_upper_lo & %is_upper_hi
        branch %is_upper ? shift_it() : keep_it()

    block shift_it():
        %offset: i32 = sext<i8, i32>(%c - 'A')
        %shifted: i32 = srem(%offset + %shift, 26)
        %new_offset: i8 = trunc<i32, i8>(%shifted)
        %new_c: char = bitcast<i8, char>(%new_offset) + 'A'
        return %new_c

    block keep_it():
        return %c

routine caesar_encrypt(#text: ptr<char>, %length: i64, %shift: i32, #out: ptr<char>) -> void
    block entry:
        jump loop(0)

    block loop(%i: i64):
        %done: bool = sge(%i, %length)
        branch %done ? finish() : process(%i)

    block process(%i: i64):
        %c: char := #text[%i]
        %new_c: char = caesar_shift(%c, %shift)
        #out[%i] = %new_c
        %next: i64 = %i + 1
        jump loop(%next)

    block finish():
        #out[%length] = '\0'
        return

; ============================================================
; main
; ============================================================

routine main() -> i32
    block entry:
        ; --- Binary search demo ---
        #data: ptr<array<i32, 8>> = alloca<array<i32, 8>>([1, 3, 5, 7, 9, 11, 13, 15])
        #arr: ptr<i32> = bitcast<ptr<array<i32, 8>>, ptr<i32>>(#data)

        %idx: i64 = binary_search(#arr, 8, 7)
        c_printf("Found 7 at index: %d\n", %idx)

        %idx2: i64 = binary_search(#arr, 8, 4)
        c_printf("Found 4 at index: %d\n", %idx2)

        ; --- Caesar cipher demo ---
        #buf: ptr<array<char, 16>> = alloca<array<char, 16>>
        #out: ptr<char> = bitcast<ptr<array<char, 16>>, ptr<char>>(#buf)

        caesar_encrypt("HELLO WORLD", 11, 3, #out)
        c_printf_str("Encrypted: %s\n", #out)

        return 0
```

**기대 출력:**

```
Found 7 at index: 3
Found 4 at index: -1
Encrypted: KHOOR ZRUOG
```

---

## 15. v0.1 Stdlib

### 15.1. Prelude (자동 import)

**비교 (signed):** `slt`, `sgt`, `sle`, `sge`

**비교 (unsigned):** `ult`, `ugt`, `ule`, `uge`

**비교 (float):** `feq`, `fne`, `flt`, `fgt`, `fle`, `fge`

**나눗셈 (signed):** `sdiv`, `srem`

**나눗셈 (unsigned):** `udiv`, `urem`

**Shift:** `shl`, `ashr`, `lshr`

**변환:** `sext`, `zext`, `trunc`, `bitcast`, `fptosi`, `sitofp`

**Float 산술:** `fadd`, `fsub`, `fmul`, `fdiv`

**Bit builtin:** `popcount`, `clz`, `ctz`, `bswap`

**기타:** `sizeof<T>`, `alignof<T>`

**제어:** `trap()` (`@noreturn`, lowers to `call llvm.trap()`)

전부 *external LLVM 함수* (template 으로). 약 30-40 declarations.

### 15.2. Memory

```
alloc<T>(#alloc: ptr<Allocator>, %count: i64) -> ptr<T>
free<T>(#alloc: ptr<Allocator>, #ptr: ptr<T>) -> void
realloc<T>(#alloc: ptr<Allocator>, #ptr: ptr<T>, %new_count: i64) -> ptr<T>
zero<T>(#ptr: ptr<T>, %count: i64) -> void
copy<T>(#dst: ptr<T>, #src: ptr<T>, %count: i64) -> void
```

### 15.3. Allocator

```
make_heap_allocator() -> Allocator       ; libc malloc/free 기반
```

### 15.4. List

```
List<T>                                  ; dynamic array
list_new<T>(#alloc) -> List<T>
list_push<T>(#list, %v, #alloc)
list_pop<T>(#list) -> T
list_get<T>(#list, %i) -> T
list_set<T>(#list, %i, %v)
list_length<T>(#list) -> i64
list_free<T>(#list, #alloc)
```

### 15.5. String

```
String                                   ; mutable, heap, allocator
string_new(#alloc) -> String
string_from_literal(#data, %len, #alloc) -> String
string_append(#str, %c, #alloc)
string_concat(#a, #b, #alloc) -> String
string_byte_at(#str, %i) -> char
string_byte_length(#str) -> i64
string_free(#str, #alloc)
```

### 15.6. UTF-8 Helpers

```
utf8_decode(#bytes: ptr<char>, %pos: i64) -> (u32, i64)    ; (codepoint, width)
utf8_encode(%cp: u32, #out: ptr<char>) -> i64              ; bytes written
```

(주의: u32 는 prelude 가 i32 alias 또는 별도 declaration 으로 처리)

### 15.7. Char 분류

```
is_ascii_digit(%c: char) -> bool
is_ascii_alpha(%c: char) -> bool
is_ascii_space(%c: char) -> bool
is_ascii_upper(%c: char) -> bool
is_ascii_lower(%c: char) -> bool
to_ascii_upper(%c: char) -> char
to_ascii_lower(%c: char) -> char
```

### 15.8. I/O

```
print(#str: ptr<char>, %len: i64)
println(#str: ptr<char>, %len: i64)
print_int(%n: i64)
print_char(%c: char)
```

### 15.9. 수학

```
abs<T>(%x: T) -> T
min<T>(%a: T, %b: T) -> T
max<T>(%a: T, %b: T) -> T
```

**총량 추정:** 1000-2000 줄 (prelude 포함).

---

## 16. Compiler Architecture

### 16.1. Components

```
Source (.disl)
    ↓ Lexer
Tokens
    ↓ Parser
AST
    ↓ Semantic analysis (type check, generic instantiation)
Typed AST
    ↓ Code generation
LLVM IR
    ↓ LLVM
Machine code
```

### 16.2. Compiler core size

- Lexer: 키워드 15개, 연산자 9개, sigil 3개 식별
- Parser: grammar 작음 (block parameter, generic 함수 호출)
- Semantic: type check, monomorphization
- Codegen: LLVM IR 생성, template 적용

**작업량 추정 (C# 사용):**

- Lexer: 1-2 주
- Parser: 2-3 주
- Semantic: 1-2 주
- Codegen: 1-2 주
- Stdlib: 1 주
- 총: **5-8 주 (1-2 개월)**

### 16.3. Implementation 언어 후보

**권유: C#**

- 컴파일러 영역 친화 (라이브러리, type system)
- 작업 빠름
- LLVMSharp (LLVM C# binding)
- 친구의 C 익숙 → 학습 작음

**대안:**

- C (친구 익숙, 작업 더 오래)
- Rust (적합하지만 학습)
- OCaml/F# (전통적 컴파일러 언어)

### 16.4. Backend

**v0.1: LLVM IR**

이유:

- Disl 와 구조 1:1 매핑 (block, branch, alloca, phi → block parameter)
- SSA 보존
- 정체성 일관 (IR 감성 언어 → IR backend)
- 강력한 옵티마이저

**옵션:**

- Text 생성 (string concatenation, 단순)
- LLVM C API (LLVMSharp 통해, type-safe)

**v0.5+:** C transpile, Cranelift 등 추가 가능

---

## 17. Roadmap

### v0.1 (목표 6-7월 2026)

- 위 사양 구현
- C# 또는 C 로 컴파일러 작성
- LLVM IR backend
- Stdlib v0.1 (prelude + 기본 자료구조)
- Sample programs (입문 예제 + 5-10개 추가)
- README, 문서

### v0.2-0.4

- 사용자 피드백
- 작은 개선 (놓친 sugar, 버그 수정)
- 추가 stdlib (HashMap 간단 버전)
- IDE 도구 (VS Code, vim)

### v0.5

- Atomic<T> + memory ordering
- Threading primitives (thread, yield)
- Mutex (futex 또는 spinlock)
- Macro 시스템 (사용자 idiom 정의)
- Runtime interface/vtable system (optional)
- Method call sugar 옵션 (`obj.func()` → `Type_func(obj)`)
- C transpile backend (옵션)

### v1.0

- Self-hosting (자기 자신으로 컴파일러 작성)
- 안정 사양
- 큰 자료구조 (BTreeMap 등) — bindings 또는 사용자 작성
- 풍부한 stdlib

---

## 18. 평가 기준

### 18.1. Verbosity

| 코드                 | 추정                        |
|--------------------|---------------------------|
| 작은 알고리즘 (sort)     | C 의 1-1.5배                |
| 자료구조 helper        | C 의 1.0-1.3배              |
| Loop 많은 코드         | C 의 2-3배                  |
| BTreeSet 같은 큰 자료구조 | C 의 1.5-2배 (~2000-3000 줄) |

**검증 방법:** v0.1 출시 후 사용자 코드 측정.

### 18.2. 학습 곡선

- Block parameter idiom: 며칠
- Sigil + memory ops: 며칠
- 전체 사양: 1-2 주

C 사용자 친화 (시스템 영역 익숙).

### 18.3. 시장

- **Primary:** 컴파일러 작가, IR 학습/연구
- **Secondary:** 시스템 도구 작가, 작은 인터프리터 작성
- **NOT:** 일반 애플리케이션, 큰 프로젝트

작은 시장이지만 *경쟁 없음*. 명확한 정체성.

---

## 19. 디자인 변경 이력

### v11 → v12 (이 문서)

- **IR 가치 정의 명확화** (1.2 섹션)
- IR 가치 = SSA + 명시 control flow + 명시 메모리 + 비용 visibility
- 어떤 features 가 IR 가치 유지 (compile time sugar) vs 깸 (runtime hidden)
- **Disl 의 sweet spot 명시** — IR 모든 가치 + 현대 sugar
- 이전 framing 정정 — generic/method sugar 가 *IR 가치 안 깸*

### v10 → v11 (이 문서)

- **Trade-off framework 명시화** — "어느 게 더 poison?" 의 디자인 sense
- 1.4 섹션 추가 (디자인 결정의 일반 framework)
- Generic 의 *2026 년 표준* 인식 — 없는 게 진짜 poison
- Mangling 의 *작업 부담* 보다 generic 의 *언어 가치* 우선

### v9 → v10 (이 문서)

- **Inline terminator** in branch/select/branch_table
- `branch %cond ? a() : trap` (별도 block 안 만듦)
- `select: ... _ -> unreachable`
- `branch %cond ? a() : return -1` (early return)
- 흔한 패턴 (bounds check, null check, exhaustive switch) 짧음

### v8 → v9 (이 문서)

- **Mangling 단순화** — LLVM IR 의 quoted identifier 활용
- Source name 그대로 symbol 로 (`@"_D::List<i32>::push"`)
- C++ 의 복잡 mangling 회피 (역사적 제약)
- 사용자 친화 (demangle 도구 불필요)
- 컴파일러 작업 더 단순 (mangling 함수 trivial)

### v7 → v8 (이 문서)

- **Namespace + method call sugar 도입** (`Type<T>::method`, `#obj.method()`)
- **`Self` keyword** (type substitution)
- **정적 dispatch** — vtable 없음, compile time resolution
- **OOP 메커니즘 거부 유지** — 가상 dispatch, 상속, 캡슐화 없음
- Token 추가: `.`, `::`
- **Name mangling 표준화** — `_D$` prefix, 명확한 규칙, cross-compiler 호환
- **`@export` attribute** — unmangled name (C 호환)

### v6 → v7

- **Unsigned framing 정정** — type-level 제거, *operation-level 유지*
- LLVM IR 모델 채택 — `i32` 가 비트 패턴, `slt`/`ult` 등이 의미 결정
- Prelude 에 `ult`, `ugt`, `ule`, `uge`, `udiv`, `urem` 추가

### v5 → v6

- **Unsigned type 제거** (`u8`, `u16`, `u32`, `u64`) — signed 충분
- **`declare` 키워드 제거** — `routine + @external` 로 통합
- **Operations 을 prelude 로 이동** — `sext`, `slt`, `sdiv` 등이 *external LLVM 함수*
- **Compiler core 키워드 ~15** 로 축소 (이전 30+)
- **Backend 결정** — LLVM IR 우선, C transpile 은 v0.5+
- **Implementation 언어 권유** — C#

### v4 → v5

- 입문 예제 추가 (binary search + caesar cipher)
- Char/String 디자인 명확
- Array literal sugar
- `?:` value select
- `unreachable`, `trap` 추가
- Mutex/atomic 은 v0.5+ 명시

---

*Designed for the niche between LLVM IR and systems languages — explicit, small, and unique.*
