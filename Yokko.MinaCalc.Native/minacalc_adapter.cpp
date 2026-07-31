#include "MinaCalc.h"
#include "MinaCalcHelpers.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <exception>
#include <vector>

#if defined(_WIN32)
#if defined(YOKKO_MINACALC_BUILD)
#define YOKKO_MINACALC_API __declspec(dllexport)
#else
#define YOKKO_MINACALC_API __declspec(dllimport)
#endif
#else
#define YOKKO_MINACALC_API __attribute__((visibility("default")))
#endif

namespace
{
constexpr std::uint32_t abi_version = 1;
constexpr std::size_t skillset_count = NUM_Skillset;

enum result_code : std::int32_t
{
    result_ok = 0,
    result_invalid_argument = 1,
    result_invalid_chart = 2,
    result_calculation_failed = 3,
};

struct msd_output
{
    std::uint32_t struct_size;
    std::array<float, skillset_count> skillsets;
};

bool notes_fit_keycount(std::uint32_t notes, std::uint32_t keycount)
{
    if (keycount >= 32)
        return true;

    return (notes >> keycount) == 0;
}

std::uint32_t remove_ignored_middle_column(
    std::uint32_t notes,
    std::uint32_t keycount)
{
    if (keycount % 2 == 0)
        return notes;

    return notes & ~(std::uint32_t{1} << (keycount / 2));
}
}

extern "C"
{
YOKKO_MINACALC_API std::uint32_t
yokko_minacalc_get_abi_version()
{
    return abi_version;
}

YOKKO_MINACALC_API std::int32_t
yokko_minacalc_get_version()
{
    return GetCalcVersion();
}

YOKKO_MINACALC_API std::int32_t
yokko_minacalc_calculate(
    const NoteInfo* notes,
    std::size_t note_count,
    std::uint32_t keycount,
    float music_rate,
    msd_output* output)
{
    if (notes == nullptr
        || output == nullptr
        || output->struct_size < sizeof(msd_output)
        || note_count <= 1
        || keycount == 0
        || keycount > 32
        || !std::isfinite(music_rate)
        || music_rate <= 0.F)
    {
        return result_invalid_argument;
    }

    std::vector<NoteInfo> prepared;
    prepared.reserve(note_count);
    float previous_time = -1.F;

    for (std::size_t index = 0; index < note_count; ++index)
    {
        const NoteInfo& note = notes[index];
        if (note.notes == 0
            || !notes_fit_keycount(note.notes, keycount)
            || !std::isfinite(note.rowTime)
            || note.rowTime < 0.F
            || note.rowTime < previous_time)
        {
            return result_invalid_chart;
        }

        NoteInfo prepared_note = note;
        prepared_note.notes = remove_ignored_middle_column(
            note.notes,
            keycount);
        if (prepared_note.notes != 0)
            prepared.push_back(prepared_note);

        previous_time = note.rowTime;
    }

    if (prepared.size() <= 1)
        return result_invalid_chart;

    try
    {
        Calc calculator;
        calculator.ssr = false;
        calculator.debugmode = false;
        calculator.keycount = keycount;

        std::vector<float> result = calculator.CalcMain(
            prepared,
            music_rate,
            default_score_goal);
        if (result.size() != skillset_count
            || std::any_of(
                result.begin(),
                result.end(),
                [](float value)
                {
                    return !std::isfinite(value) || value < 0.F;
                }))
        {
            return result_calculation_failed;
        }

        std::copy(result.begin(), result.end(), output->skillsets.begin());
        return result_ok;
    }
    catch (const std::exception&)
    {
        return result_calculation_failed;
    }
    catch (...)
    {
        return result_calculation_failed;
    }
}
}
