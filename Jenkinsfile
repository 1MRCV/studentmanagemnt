properties([
  parameters([

    choice(
      name: 'ENVIRONMENT',
      choices: ['DEV', 'PRODUCTION'],
      description: 'Select deployment environment'
    ),

    choice(
      name: 'ACTION',
      choices: ['DEPLOY', 'ROLLBACK'],
      description: 'DEPLOY = build and deploy (DEV) or deploy existing artifact (PRODUCTION). ROLLBACK = redeploy a previous build.'
    ),

    string(
      name: 'BRANCH',
      defaultValue: 'main',
      description: 'Git branch to build — only used when ENVIRONMENT=DEV and ACTION=DEPLOY'
    ),

    [$class: 'CascadeChoiceParameter',
      name: 'ARTIFACT_BUILD',
      description: 'Select build to deploy — shows all successful DEV builds',
      choiceType: 'PT_SINGLE_SELECT',
      filterable: true,
      script: [
        $class: 'GroovyScript',
        script: [
          $class: 'SecureGroovyScript',
          sandbox: false,
          script: '''
            import jenkins.model.Jenkins

            def job = Jenkins.instance.getItemByFullName("Jenkins-only")

            if (job == null) {
              return ["ERROR: Job not found"]
            }

            def list = []

            job.getBuilds().each { build ->
              if (build.getResult()?.toString() == "SUCCESS") {
                def params   = build.getAction(hudson.model.ParametersAction)
                def envParam = params?.getParameter("ENVIRONMENT")?.getValue()
                def actParam = params?.getParameter("ACTION")?.getValue()

                if (envParam == "DEV" && actParam == "DEPLOY") {
                  def date   = new java.text.SimpleDateFormat("yyyy-MM-dd HH:mm").format(build.getTime())
                  def branch = build.getEnvironment()?.get("GIT_BRANCH") ?: "unknown"
                  branch     = branch.replaceAll("^origin/", "")
                  list << "Build_${build.getNumber()} | ${date} | ${branch}"
                }
              }
            }

            return list.isEmpty()
              ? ["No DEV builds yet — run ENVIRONMENT=DEV, ACTION=DEPLOY first"]
              : list
          '''
        ]
      ]
    ]

  ])
])

pipeline {

  agent { label 'windows-agent' }

  // Prevents Jenkins from automatically doing a git checkout
  // on the Windows agent before stages run (which would fail
  // because it tries to use /usr/bin/git on Windows)
  options {
    skipDefaultCheckout(true)
  }

  environment {
    GIT_REPO      = 'https://github.com/1MRCV/studentmanagemnt.git'

    DEV_ARTIFACT_STORAGE  = 'C:\\jenkins-artifacts\\DEV'
    PROD_ARTIFACT_STORAGE = 'C:\\jenkins-artifacts\\PROD'

    DEV_IIS_PATH   = 'C:\\inetpub\\dev'
    PROD_IIS_PATH  = 'C:\\inetpub\\prod'

    DEV_APP_POOL   = 'student-dev-pool'
    PROD_APP_POOL  = 'student-prod-pool'

    DEV_SITE       = 'student-dev'
    PROD_SITE      = 'student-prod'

    DEV_PORT       = '8081'
    PROD_PORT      = '8082'

    PUBLISH_FOLDER = 'publish'
  }

  stages {

    stage('Set Build Name') {
      steps {
        script {
          def date = new Date().format('yyyy-MM-dd')
          currentBuild.displayName = "#${env.BUILD_NUMBER} | ${params.ENVIRONMENT} | ${params.ACTION} | ${date}"
        }
      }
    }

    stage('Clean Workspace') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION      == 'DEPLOY' }
        }
      }
      steps {
        deleteDir()
      }
    }

    stage('Checkout Code') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION      == 'DEPLOY' }
        }
      }
      steps {
        checkout([
          $class: 'GitSCM',
          branches: [[name: "${params.BRANCH}"]],
          userRemoteConfigs: [[url: "${env.GIT_REPO}"]],
          gitTool: 'Git-Windows'
        ])
      }
    }

    stage('Restore Dependencies') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION      == 'DEPLOY' }
        }
      }
      steps {
        powershell 'dotnet restore'
      }
    }

    stage('Build Application') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION      == 'DEPLOY' }
        }
      }
      steps {
        powershell 'dotnet build --configuration Release'
      }
    }

    stage('Publish Website') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION      == 'DEPLOY' }
        }
      }
      steps {
        powershell 'dotnet publish StudentPortal.Web/StudentPortal.Web.csproj -c Release -o publish'
      }
    }

    stage('Create Artifact Version') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION      == 'DEPLOY' }
        }
      }
      steps {
        powershell '''
          $build    = $env:BUILD_NUMBER
          $date     = Get-Date -Format "yyyy-MM-dd"
          $dateTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
          $storage  = $env:DEV_ARTIFACT_STORAGE

          if (!(Test-Path $storage)) {
            New-Item -ItemType Directory -Path $storage | Out-Null
          }

          $folder = "$storage\\build_${build}_$date"
          New-Item -ItemType Directory -Path $folder -Force | Out-Null

          Write-Host "Creating artifact: $folder"

          Compress-Archive `
            -Path "$env:PUBLISH_FOLDER\\*" `
            -DestinationPath "$folder\\artifact.zip" `
            -Force

          $commit = git rev-parse --short HEAD
          $branch = git rev-parse --abbrev-ref HEAD

          @"
Application : StudentPortal
Build       : $build
Branch      : $branch
Commit      : $commit
Date        : $dateTime
"@ | Out-File "$folder\\build-info.txt" -Encoding UTF8

          Write-Host "Artifact stored at $folder"
        '''
      }
    }

    stage('Promote Artifact to PROD') {
      when {
        expression { params.ENVIRONMENT == 'PRODUCTION' }
      }
      steps {
        powershell """
          \$selected  = "${params.ARTIFACT_BUILD}"
          \$buildNum  = \$selected.Split("|")[0].Replace("Build_","").Trim()

          \$devStorage  = "\$env:DEV_ARTIFACT_STORAGE"
          \$prodStorage = "\$env:PROD_ARTIFACT_STORAGE"

          \$devFolder = Get-ChildItem \$devStorage -Directory -ErrorAction SilentlyContinue |
                        Where-Object { \$_.Name -like "build_\${buildNum}_*" } |
                        Select-Object -First 1

          if (\$null -eq \$devFolder) {
            Write-Error "Build_\$buildNum not found in DEV artifacts."
            exit 1
          }

          if (!(Test-Path \$prodStorage)) {
            New-Item -ItemType Directory -Path \$prodStorage -Force | Out-Null
          }

          \$prodFolder = Join-Path \$prodStorage \$devFolder.Name

          if (Test-Path \$prodFolder) {
            Write-Host "Artifact already exists in PROD: \$(\$devFolder.Name)"
          } else {
            Copy-Item -Path \$devFolder.FullName -Destination \$prodStorage -Recurse -Force
            Write-Host "Promoted \$(\$devFolder.Name) from DEV to PROD."
          }
        """
      }
    }

    stage('Deploy to IIS') {
      steps {
        powershell """
          \$ErrorActionPreference = "Stop"
          Import-Module WebAdministration

          \$envName  = "${params.ENVIRONMENT}"
          \$selected = "${params.ARTIFACT_BUILD}"

          \$storage = if (\$envName -eq "DEV") { "\$env:DEV_ARTIFACT_STORAGE" } else { "\$env:PROD_ARTIFACT_STORAGE" }

          if ([string]::IsNullOrWhiteSpace(\$selected) -or \$selected -like "No DEV*" -or \$selected -like "ERROR:*") {
            Write-Host "No artifact selected — using latest."
            \$folder = Get-ChildItem \$storage -Directory |
                       Sort-Object LastWriteTime -Descending |
                       Select-Object -First 1
          } else {
            \$buildNum = \$selected.Split("|")[0].Replace("Build_","").Trim()
            \$folder   = Get-ChildItem \$storage -Directory |
                         Where-Object { \$_.Name -like "build_\${buildNum}_*" } |
                         Select-Object -First 1
          }

          if (\$null -eq \$folder) {
            throw "Artifact not found in \$storage. Run a DEV build first."
          }

          \$zipPath = Join-Path \$folder.FullName "artifact.zip"

          if (!(Test-Path \$zipPath)) {
            throw "artifact.zip not found in \$(\$folder.FullName)"
          }

          Write-Host "Deploying build: \$(\$folder.Name)"

          if (\$envName -eq "DEV") {
            \$deployPath  = "\$env:DEV_IIS_PATH"
            \$appPool     = "\$env:DEV_APP_POOL"
            \$websiteName = "\$env:DEV_SITE"
            \$websitePort = "\$env:DEV_PORT"
          } else {
            \$deployPath  = "\$env:PROD_IIS_PATH"
            \$appPool     = "\$env:PROD_APP_POOL"
            \$websiteName = "\$env:PROD_SITE"
            \$websitePort = "\$env:PROD_PORT"
          }

          if (!(Test-Path "IIS:\\AppPools\\\$appPool")) {
            Write-Host "Creating Application Pool: \$appPool"
            New-WebAppPool -Name \$appPool
          } else {
            Write-Host "Application Pool already exists: \$appPool"
          }

          if (!(Test-Path "IIS:\\Sites\\\$websiteName")) {
            Write-Host "Creating IIS Website: \$websiteName"
            if (!(Test-Path \$deployPath)) {
              New-Item -ItemType Directory -Path \$deployPath | Out-Null
            }
            New-Website `
              -Name \$websiteName `
              -Port \$websitePort `
              -PhysicalPath \$deployPath `
              -ApplicationPool \$appPool
          } else {
            Write-Host "Website already exists: \$websiteName"
          }

          \$state = (Get-WebAppPoolState -Name \$appPool).Value
          if (\$state -eq "Started") {
            Write-Host "Stopping App Pool..."
            Stop-WebAppPool -Name \$appPool
            Start-Sleep -Seconds 3
          }

          if (!(Test-Path \$deployPath)) {
            New-Item -ItemType Directory -Path \$deployPath | Out-Null
          }

          Write-Host "Cleaning old files..."
          Remove-Item "\$deployPath\\*" -Recurse -Force -ErrorAction SilentlyContinue

          Write-Host "Extracting build..."
          Expand-Archive -Path \$zipPath -DestinationPath \$deployPath -Force

          Write-Host "Starting App Pool and Website..."
          Start-WebAppPool -Name \$appPool
          Start-Website    -Name \$websiteName

          Write-Host "Deployment completed successfully."
        """
      }
    }

    stage('Verify Deployment') {
      steps {
        powershell """
          Import-Module WebAdministration

          \$appPool = if ("${params.ENVIRONMENT}" -eq "DEV") { "\$env:DEV_APP_POOL" } else { "\$env:PROD_APP_POOL" }

          \$state = (Get-WebAppPoolState -Name \$appPool).Value
          Write-Host "App Pool State: \$state"

          if (\$state -ne "Started") {
            throw "Deployment verification failed — App Pool not running."
          }

          Write-Host "Deployment verified successfully."
        """
      }
    }

  }

  post {
    success {
      echo "Build ${BUILD_NUMBER} deployed successfully."
    }
    failure {
      echo "Deployment failed. Please check logs."
    }
  }

}
