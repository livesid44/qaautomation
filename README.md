# QA Automation System

A .NET 8 based quality automation system with support for designing custom evaluation forms.

## Features

- **Custom Evaluation Form Designer**: Create forms with multiple sections and configurable field types (Text, TextArea, Rating, Checkbox, Dropdown, Number)
- **Evaluation Results**: Submit and track evaluation results with per-field scores
- **REST API**: Full CRUD API with Swagger/OpenAPI documentation
- **Soft Delete**: Forms are soft-deleted (marked inactive) to preserve evaluation history

## Project Structure

```
QAAutomation.sln
├── src/
│   └── QAAutomation.API/       # ASP.NET Core Web API
│       ├── Controllers/        # EvaluationForms, EvaluationResults
│       ├── Data/               # EF Core DbContext (SQLite)
│       ├── DTOs/               # Request/response data transfer objects
│       └── Models/             # Domain models
└── tests/
    └── QAAutomation.Tests/     # xUnit integration tests
```

## Getting Started

### Prerequisites
- .NET 8 SDK

### Run the API

```bash
cd src/QAAutomation.API
dotnet run
```

The API will be available at `http://localhost:5018`. Open `http://localhost:5018/swagger` to explore the API.

### Run Tests

```bash
dotnet test QAAutomation.sln
```

## API Endpoints

### Evaluation Forms

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/evaluationforms` | List all active forms |
| GET | `/api/evaluationforms/{id}` | Get form with sections and fields |
| POST | `/api/evaluationforms` | Create a new custom evaluation form |
| PUT | `/api/evaluationforms/{id}` | Update a form |
| DELETE | `/api/evaluationforms/{id}` | Soft delete a form |

### Evaluation Results

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/evaluationresults` | List all results |
| GET | `/api/evaluationresults/{id}` | Get result with scores |
| POST | `/api/evaluationresults` | Submit an evaluation |
| GET | `/api/evaluationresults/byform/{formId}` | Get results for a form |

## Example: Create a Custom Evaluation Form

```json
POST /api/evaluationforms
{
  "name": "Code Review Checklist",
  "description": "Standard code review evaluation form",
  "sections": [
    {
      "title": "Code Quality",
      "order": 1,
      "fields": [
        { "label": "Readability", "fieldType": 2, "isRequired": true, "order": 1, "maxRating": 5 },
        { "label": "Test Coverage", "fieldType": 2, "isRequired": true, "order": 2, "maxRating": 5 },
        { "label": "Comments", "fieldType": 1, "isRequired": false, "order": 3 }
      ]
    }
  ]
}
```

Field types: `0`=Text, `1`=TextArea, `2`=Rating, `3`=Checkbox, `4`=Dropdown, `5`=Number